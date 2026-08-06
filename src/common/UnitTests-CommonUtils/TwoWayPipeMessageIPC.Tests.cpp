#include "pch.h"

#include <interop/two_way_pipe_message_ipc.h>
#include <aclapi.h>

#include <memory>
#include <system_error>
#include <thread>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    namespace
    {
        constexpr DWORD PipeClientAccess = FILE_READ_DATA |
                                           FILE_READ_ATTRIBUTES |
                                           READ_CONTROL |
                                           FILE_WRITE_DATA |
                                           FILE_WRITE_ATTRIBUTES |
                                           SYNCHRONIZE;

        std::wstring UniquePipeName()
        {
            static LONG counter = 0;
            return L"\\\\.\\pipe\\pt_ipc_test_" +
                   std::to_wstring(GetCurrentProcessId()) + L"_" +
                   std::to_wstring(GetTickCount64()) + L"_" +
                   std::to_wstring(InterlockedIncrement(&counter));
        }

        HANDLE ConnectPipeClient(const std::wstring& pipe_name)
        {
            constexpr DWORD timeout_ms = 2'000;
            const ULONGLONG deadline = GetTickCount64() + timeout_ms;
            do
            {
                HANDLE client = CreateFileW(pipe_name.c_str(),
                                            PipeClientAccess,
                                            0,
                                            nullptr,
                                            OPEN_EXISTING,
                                            0,
                                            nullptr);
                if (client != INVALID_HANDLE_VALUE)
                {
                    return client;
                }

                const DWORD error = GetLastError();
                if (error != ERROR_FILE_NOT_FOUND && error != ERROR_PIPE_BUSY)
                {
                    return INVALID_HANDLE_VALUE;
                }
                WaitNamedPipeW(pipe_name.c_str(), 50);
            } while (GetTickCount64() < deadline);

            SetLastError(ERROR_SEM_TIMEOUT);
            return INVALID_HANDLE_VALUE;
        }

        struct RestrictedClientToken
        {
            HANDLE token = nullptr;

            ~RestrictedClientToken()
            {
                if (token)
                {
                    CloseHandle(token);
                }
            }

            bool Create()
            {
                HANDLE process_token = nullptr;
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY | TOKEN_DUPLICATE, &process_token))
                {
                    return false;
                }

                DWORD user_size = 0;
                GetTokenInformation(process_token, TokenUser, nullptr, 0, &user_size);
                std::vector<BYTE> user_buffer(user_size);
                if (!GetTokenInformation(process_token, TokenUser, user_buffer.data(), user_size, &user_size))
                {
                    CloseHandle(process_token);
                    return false;
                }

                auto* user = reinterpret_cast<TOKEN_USER*>(user_buffer.data());
                SID_AND_ATTRIBUTES disabled_sid{ user->User.Sid, 0 };
                HANDLE restricted_primary_token = nullptr;
                const BOOL restricted = CreateRestrictedToken(process_token,
                                                              0,
                                                              1,
                                                              &disabled_sid,
                                                              0,
                                                              nullptr,
                                                              0,
                                                              nullptr,
                                                              &restricted_primary_token);
                CloseHandle(process_token);
                if (!restricted)
                {
                    return false;
                }

                const BOOL duplicated = DuplicateTokenEx(restricted_primary_token,
                                                         TOKEN_QUERY | TOKEN_IMPERSONATE,
                                                         nullptr,
                                                         SecurityImpersonation,
                                                         TokenImpersonation,
                                                         &token);
                CloseHandle(restricted_primary_token);
                return duplicated == TRUE;
            }
        };

        struct NormalSameUserClientToken
        {
            HANDLE token = nullptr;

            ~NormalSameUserClientToken()
            {
                if (token)
                {
                    CloseHandle(token);
                }
            }

            bool Create()
            {
                HANDLE process_token = nullptr;
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY | TOKEN_DUPLICATE, &process_token))
                {
                    return false;
                }

                BYTE administrators_sid[SECURITY_MAX_SID_SIZE]{};
                DWORD administrators_sid_size = ARRAYSIZE(administrators_sid);
                if (!CreateWellKnownSid(WinBuiltinAdministratorsSid,
                                        nullptr,
                                        administrators_sid,
                                        &administrators_sid_size))
                {
                    CloseHandle(process_token);
                    return false;
                }

                DWORD groups_size = 0;
                GetTokenInformation(process_token, TokenGroups, nullptr, 0, &groups_size);
                std::vector<BYTE> groups_buffer(groups_size);
                if (!GetTokenInformation(process_token, TokenGroups, groups_buffer.data(), groups_size, &groups_size))
                {
                    CloseHandle(process_token);
                    return false;
                }

                const auto* groups = reinterpret_cast<const TOKEN_GROUPS*>(groups_buffer.data());
                SID_AND_ATTRIBUTES disabled_administrators_sid{};
                DWORD disable_count = 0;
                for (DWORD index = 0; index < groups->GroupCount; ++index)
                {
                    if (EqualSid(groups->Groups[index].Sid, administrators_sid))
                    {
                        disabled_administrators_sid.Sid = groups->Groups[index].Sid;
                        disable_count = 1;
                        break;
                    }
                }

                HANDLE restricted_primary_token = nullptr;
                const BOOL restricted = CreateRestrictedToken(process_token,
                                                              0,
                                                              disable_count,
                                                              disable_count ? &disabled_administrators_sid : nullptr,
                                                              0,
                                                              nullptr,
                                                              0,
                                                              nullptr,
                                                              &restricted_primary_token);
                CloseHandle(process_token);
                if (!restricted)
                {
                    return false;
                }

                const BOOL duplicated = DuplicateTokenEx(restricted_primary_token,
                                                         TOKEN_QUERY | TOKEN_IMPERSONATE,
                                                         nullptr,
                                                         SecurityImpersonation,
                                                         TokenImpersonation,
                                                         &token);
                CloseHandle(restricted_primary_token);
                return duplicated == TRUE;
            }
        };

        struct ScopedImpersonation
        {
            explicit ScopedImpersonation(HANDLE token) :
                active(ImpersonateLoggedOnUser(token) == TRUE)
            {
            }

            ~ScopedImpersonation()
            {
                if (active)
                {
                    RevertToSelf();
                }
            }

            bool active = false;
        };

        struct FaultInjectionReset
        {
            FaultInjectionReset()
            {
                two_way_pipe_message_ipc_test::ResetFaultInjection();
            }

            ~FaultInjectionReset()
            {
                two_way_pipe_message_ipc_test::ResetFaultInjection();
            }
        };

        bool LogonSidPipeAceAllowsInstanceCreation(HANDLE pipe,
                                                   HANDLE token,
                                                   bool& allows_client_access,
                                                   DWORD& matching_access_mask,
                                                   DWORD& error)
        {
            allows_client_access = false;
            matching_access_mask = 0;
            DWORD groups_size = 0;
            GetTokenInformation(token, TokenGroups, nullptr, 0, &groups_size);
            std::vector<BYTE> groups_buffer(groups_size);
            if (!GetTokenInformation(token, TokenGroups, groups_buffer.data(), groups_size, &groups_size))
            {
                error = GetLastError();
                return false;
            }

            const auto* groups = reinterpret_cast<const TOKEN_GROUPS*>(groups_buffer.data());
            PSID logon_sid = nullptr;
            for (DWORD index = 0; index < groups->GroupCount; ++index)
            {
                if ((groups->Groups[index].Attributes & SE_GROUP_LOGON_ID) == SE_GROUP_LOGON_ID)
                {
                    logon_sid = groups->Groups[index].Sid;
                    break;
                }
            }
            if (!logon_sid)
            {
                error = ERROR_NOT_FOUND;
                return false;
            }

            PSECURITY_DESCRIPTOR security_descriptor = nullptr;
            PACL dacl = nullptr;
            const DWORD security_result = GetSecurityInfo(pipe,
                                                          SE_KERNEL_OBJECT,
                                                          DACL_SECURITY_INFORMATION,
                                                          nullptr,
                                                          nullptr,
                                                          &dacl,
                                                          nullptr,
                                                          &security_descriptor);
            if (security_result != ERROR_SUCCESS)
            {
                error = security_result;
                return false;
            }

            bool allows_creation = false;
            ACL_SIZE_INFORMATION acl_info{};
            if (!GetAclInformation(dacl, &acl_info, sizeof(acl_info), AclSizeInformation))
            {
                error = GetLastError();
                LocalFree(security_descriptor);
                return false;
            }

            for (DWORD index = 0; index < acl_info.AceCount; ++index)
            {
                void* ace = nullptr;
                if (!GetAce(dacl, index, &ace))
                {
                    error = GetLastError();
                    LocalFree(security_descriptor);
                    return false;
                }

                auto* allowed_ace = static_cast<ACCESS_ALLOWED_ACE*>(ace);
                if (allowed_ace->Header.AceType != ACCESS_ALLOWED_ACE_TYPE ||
                    !EqualSid(logon_sid, reinterpret_cast<PSID>(&allowed_ace->SidStart)))
                {
                    continue;
                }

                const DWORD access_mask = allowed_ace->Mask;
                matching_access_mask |= access_mask;
                allows_creation |= (access_mask & (GENERIC_WRITE | FILE_CREATE_PIPE_INSTANCE)) != 0;
                allows_client_access |= (access_mask & PipeClientAccess) == PipeClientAccess;
            }
            LocalFree(security_descriptor);
            error = ERROR_SUCCESS;
            return allows_creation;
        }

        struct OccupiedPipe
        {
            std::wstring name = UniquePipeName();
            HANDLE server = INVALID_HANDLE_VALUE;
            HANDLE client = INVALID_HANDLE_VALUE;

            ~OccupiedPipe()
            {
                if (client != INVALID_HANDLE_VALUE)
                {
                    CloseHandle(client);
                }
                if (server != INVALID_HANDLE_VALUE)
                {
                    CloseHandle(server);
                }
            }

            bool Create()
            {
                server = CreateNamedPipeW(name.c_str(),
                                          PIPE_ACCESS_DUPLEX,
                                          PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
                                          PIPE_UNLIMITED_INSTANCES,
                                          4096,
                                          4096,
                                          0,
                                          nullptr);
                if (server == INVALID_HANDLE_VALUE)
                {
                    return false;
                }

                std::thread connectThread([&]() {
                    client = CreateFileW(name.c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, 0, nullptr);
                });
                const BOOL connected = ConnectNamedPipe(server, nullptr) ? TRUE : (GetLastError() == ERROR_PIPE_CONNECTED);
                connectThread.join();
                return connected && client != INVALID_HANDLE_VALUE;
            }
        };

        struct BlockedRejectedConnection
        {
            HANDLE client = INVALID_HANDLE_VALUE;
            HANDLE handler_entered = CreateEventW(nullptr, TRUE, FALSE, nullptr);
            HANDLE allow_handler_to_finish = CreateEventW(nullptr, TRUE, FALSE, nullptr);

            ~BlockedRejectedConnection()
            {
                if (client != INVALID_HANDLE_VALUE)
                {
                    CloseHandle(client);
                }
                if (handler_entered)
                {
                    CloseHandle(handler_entered);
                }
                if (allow_handler_to_finish)
                {
                    CloseHandle(allow_handler_to_finish);
                }
            }

            bool Start(TwoWayPipeMessageIPC& server, const std::wstring& input_pipe_name)
            {
                interop_auth::CallerPolicy policy;
                policy.enabled = true;
                policy.expectedDirectory = L"Z:\\not-the-test-host";
                policy.allowedBasenames = { L"not-the-test-host.exe" };
                policy.requireMicrosoftSignature = false;
                policy.logReject = [this](const interop_auth::AuthResult&) {
                    SetEvent(handler_entered);
                    WaitForSingleObject(allow_handler_to_finish, 10'000);
                };

                server.start(nullptr, policy);
                client = ConnectPipeClient(input_pipe_name);
                return client != INVALID_HANDLE_VALUE &&
                       WaitForSingleObject(handler_entered, 2'000) == WAIT_OBJECT_0;
            }

            void AllowHandlerToFinish()
            {
                SetEvent(allow_handler_to_finish);
            }
        };
    }

    TEST_CLASS(TwoWayPipeMessageIPCTests)
    {
    public:
        TEST_METHOD(ServerDoesNotJoinAnExistingPipeName)
        {
            OccupiedPipe occupiedPipe;
            Assert::IsTrue(occupiedPipe.Create(), L"failed to occupy the pipe name");

            TwoWayPipeMessageIPC server(occupiedPipe.name, UniquePipeName(), nullptr);
            server.start(nullptr);

            // The existing instance is busy. A server that wrongly creates a second instance makes
            // WaitNamedPipe succeed; FILE_FLAG_FIRST_PIPE_INSTANCE must instead make its first
            // CreateNamedPipe call fail and leave no available instance.
            const BOOL available = WaitNamedPipeW(occupiedPipe.name.c_str(), 2000);
            server.end();

            Assert::IsFalse(available, L"the server must not join an existing pipe name");
        }

        TEST_METHOD(RestrictedClientCanConnectButCannotCreateAnotherServerInstance)
        {
            HANDLE token = nullptr;
            Assert::IsTrue(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token) == TRUE,
                           L"failed to open the current process token");

            RestrictedClientToken restricted_client;
            Assert::IsTrue(restricted_client.Create(), L"failed to create the restricted same-logon client token");

            const std::wstring input_pipe_name = UniquePipeName();
            TwoWayPipeMessageIPC server(input_pipe_name, UniquePipeName(), nullptr);
            server.start(token);

            {
                ScopedImpersonation impersonation(restricted_client.token);
                Assert::IsTrue(impersonation.active, L"failed to impersonate the restricted client token");

                HANDLE client = ConnectPipeClient(input_pipe_name);
                const DWORD connect_error = GetLastError();
                Assert::IsTrue(client != INVALID_HANDLE_VALUE,
                               (L"the explicitly-permitted client access must connect; error=" +
                                std::to_wstring(connect_error))
                                   .c_str());

                // A later CreateNamedPipe call is authorized by the first instance's DACL. Verify
                // that the ACE for this same-logon client contains every requested client right
                // but excludes FILE_CREATE_PIPE_INSTANCE (also included by GENERIC_WRITE).
                DWORD acl_error = ERROR_SUCCESS;
                bool acl_allows_client_access = false;
                DWORD matching_access_mask = 0;
                bool can_create_later_instance = false;
                const ULONGLONG acl_deadline = GetTickCount64() + 2'000;
                do
                {
                    can_create_later_instance = LogonSidPipeAceAllowsInstanceCreation(client,
                                                                                         token,
                                                                                         acl_allows_client_access,
                                                                                         matching_access_mask,
                                                                                         acl_error);
                    if (acl_error != ERROR_SUCCESS || acl_allows_client_access)
                    {
                        break;
                    }
                    Sleep(10);
                } while (GetTickCount64() < acl_deadline);
                CloseHandle(client);

                Assert::IsTrue(acl_allows_client_access,
                               (L"the same-logon client ACE must contain the explicit client access rights; mask=" +
                                std::to_wstring(matching_access_mask))
                                   .c_str());
                Assert::IsFalse(can_create_later_instance,
                               L"a same-logon client must not create a later pipe instance");
                Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS), acl_error);
            }

            server.end();
            CloseHandle(token);
        }

        TEST_METHOD(NormalSameUserCannotModifyProtectedDaclOrCreateAnotherServerInstance)
        {
            HANDLE token = nullptr;
            Assert::IsTrue(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token) == TRUE);

            const std::wstring input_pipe_name = UniquePipeName();
            TwoWayPipeMessageIPC server(input_pipe_name, UniquePipeName(), nullptr);
            server.start(token);
            CloseHandle(token);

            NormalSameUserClientToken normal_client;
            Assert::IsTrue(normal_client.Create(), L"failed to create the normal same-user client token");

            {
                ScopedImpersonation impersonation(normal_client.token);
                Assert::IsTrue(impersonation.active, L"failed to impersonate the normal same-user client token");

                HANDLE client = ConnectPipeClient(input_pipe_name);
                Assert::IsTrue(client != INVALID_HANDLE_VALUE, L"the normal client could not connect to the protected pipe");

                PSECURITY_DESCRIPTOR security_descriptor = nullptr;
                PSID owner = nullptr;
                PACL dacl = nullptr;
                Assert::AreEqual(static_cast<DWORD>(ERROR_SUCCESS),
                                 GetSecurityInfo(client,
                                                 SE_KERNEL_OBJECT,
                                                 OWNER_SECURITY_INFORMATION | DACL_SECURITY_INFORMATION,
                                                 &owner,
                                                 nullptr,
                                                 &dacl,
                                                 nullptr,
                                                 &security_descriptor));

                BYTE administrators_sid[SECURITY_MAX_SID_SIZE]{};
                DWORD administrators_sid_size = ARRAYSIZE(administrators_sid);
                Assert::IsTrue(CreateWellKnownSid(WinBuiltinAdministratorsSid,
                                                  nullptr,
                                                  administrators_sid,
                                                  &administrators_sid_size) == TRUE);
                Assert::IsTrue(EqualSid(owner, administrators_sid) == TRUE,
                               L"the pipe owner must not be the normal client user");

                const DWORD set_dacl_error = SetSecurityInfo(client,
                                                             SE_KERNEL_OBJECT,
                                                             DACL_SECURITY_INFORMATION,
                                                             nullptr,
                                                             nullptr,
                                                             dacl,
                                                             nullptr);
                SetLastError(ERROR_SUCCESS);
                HANDLE rogue_server = CreateNamedPipeW(input_pipe_name.c_str(),
                                                       PIPE_ACCESS_DUPLEX,
                                                       PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
                                                       PIPE_UNLIMITED_INSTANCES,
                                                       4096,
                                                       4096,
                                                       0,
                                                       nullptr);
                const DWORD create_instance_error = GetLastError();
                if (rogue_server != INVALID_HANDLE_VALUE)
                {
                    CloseHandle(rogue_server);
                }

                LocalFree(security_descriptor);
                CloseHandle(client);

                Assert::AreEqual(static_cast<DWORD>(ERROR_ACCESS_DENIED), set_dacl_error);
                Assert::IsTrue(rogue_server == INVALID_HANDLE_VALUE,
                               L"the normal same-user client created a later server instance");
                Assert::AreEqual(static_cast<DWORD>(ERROR_ACCESS_DENIED), create_instance_error);
            }

            server.end();
        }

        TEST_METHOD(StartFailureAfterFirstThreadCleansUp)
        {
            FaultInjectionReset reset;
            auto server = std::make_unique<TwoWayPipeMessageIPC>(UniquePipeName(), UniquePipeName(), nullptr);
            two_way_pipe_message_ipc_test::FailThreadStartAfter(1);

            bool threw = false;
            try
            {
                server->start(nullptr);
            }
            catch (const std::system_error&)
            {
                threw = true;
            }

            Assert::IsTrue(threw, L"the injected second thread creation failure was not observed");
            server->end();
            server.reset();
        }

        TEST_METHOD(StartFailureAfterSecondThreadCleansUp)
        {
            FaultInjectionReset reset;
            auto server = std::make_unique<TwoWayPipeMessageIPC>(UniquePipeName(), UniquePipeName(), nullptr);
            two_way_pipe_message_ipc_test::FailThreadStartAfter(2);

            bool threw = false;
            try
            {
                server->start(nullptr);
            }
            catch (const std::system_error&)
            {
                threw = true;
            }

            Assert::IsTrue(threw, L"the injected third thread creation failure was not observed");
            server->end();
            server.reset();
        }

        TEST_METHOD(EndWaitsForActiveConnectionHandler)
        {
            const std::wstring input_pipe_name = UniquePipeName();
            TwoWayPipeMessageIPC server(input_pipe_name, UniquePipeName(), nullptr);
            BlockedRejectedConnection connection;
            Assert::IsTrue(connection.Start(server, input_pipe_name),
                           L"the test connection did not enter its handler");

            HANDLE end_finished = CreateEventW(nullptr, TRUE, FALSE, nullptr);
            Assert::IsNotNull(end_finished);
            std::thread shutdown_thread([&]() {
                server.end();
                SetEvent(end_finished);
            });

            Assert::AreEqual(static_cast<DWORD>(WAIT_TIMEOUT), WaitForSingleObject(end_finished, 200),
                             L"end must wait for the active handler before returning");
            connection.AllowHandlerToFinish();
            Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(end_finished, 5'000),
                             L"end did not finish after the active handler completed");

            shutdown_thread.join();
            CloseHandle(end_finished);
        }

        TEST_METHOD(DestructorWaitsForActiveConnectionHandler)
        {
            const std::wstring input_pipe_name = UniquePipeName();
            auto server = std::make_unique<TwoWayPipeMessageIPC>(input_pipe_name, UniquePipeName(), nullptr);
            BlockedRejectedConnection connection;
            Assert::IsTrue(connection.Start(*server, input_pipe_name),
                           L"the test connection did not enter its handler");

            HANDLE destructor_finished = CreateEventW(nullptr, TRUE, FALSE, nullptr);
            Assert::IsNotNull(destructor_finished);
            std::thread destroyer([&]() {
                server.reset();
                SetEvent(destructor_finished);
            });

            Assert::AreEqual(static_cast<DWORD>(WAIT_TIMEOUT), WaitForSingleObject(destructor_finished, 200),
                             L"destruction must wait for the active handler before freeing IPC state");
            connection.AllowHandlerToFinish();
            Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(destructor_finished, 5'000),
                             L"destruction did not finish after the active handler completed");

            destroyer.join();
            CloseHandle(destructor_finished);
        }

        TEST_METHOD(DestructorCancelsBlockedConnectionRead)
        {
            const std::wstring input_pipe_name = UniquePipeName();
            auto server = std::make_unique<TwoWayPipeMessageIPC>(input_pipe_name, UniquePipeName(), nullptr);
            HANDLE server_token = nullptr;
            Assert::IsTrue(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &server_token) == TRUE);
            server->start(server_token);
            CloseHandle(server_token);

            RestrictedClientToken restricted_client;
            Assert::IsTrue(restricted_client.Create(), L"failed to create the restricted same-logon client token");

            HANDLE client = INVALID_HANDLE_VALUE;
            {
                ScopedImpersonation impersonation(restricted_client.token);
                Assert::IsTrue(impersonation.active, L"failed to impersonate the restricted client token");
                client = ConnectPipeClient(input_pipe_name);
            }
            const DWORD connect_error = GetLastError();
            Assert::IsTrue(client != INVALID_HANDLE_VALUE,
                           (L"failed to connect the client that blocks in ReadFile; error=" +
                            std::to_wstring(connect_error))
                               .c_str());

            // The next listener is created only after the accepted connection has been registered
            // for lifetime tracking, so destruction must cancel that handler's blocked read.
            Assert::IsTrue(WaitNamedPipeW(input_pipe_name.c_str(), 2'000) == TRUE,
                           L"the server did not create the next listening instance");

            HANDLE destructor_finished = CreateEventW(nullptr, TRUE, FALSE, nullptr);
            Assert::IsNotNull(destructor_finished);
            std::thread destroyer([&]() {
                server.reset();
                SetEvent(destructor_finished);
            });

            Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(destructor_finished, 5'000),
                             L"destruction did not cancel and join the handler blocked in ReadFile");

            destroyer.join();
            CloseHandle(destructor_finished);
            CloseHandle(client);
        }

        TEST_METHOD(EndInterruptsBusyOutputPipeWait)
        {
            FaultInjectionReset reset;
            OccupiedPipe busy_output_pipe;
            Assert::IsTrue(busy_output_pipe.Create(), L"failed to create the busy output pipe");

            HANDLE wait_entered = CreateEventW(nullptr, TRUE, FALSE, nullptr);
            Assert::IsNotNull(wait_entered);
            two_way_pipe_message_ipc_test::SetWaitNamedPipeEnteredEvent(wait_entered);

            TwoWayPipeMessageIPC server(UniquePipeName(), busy_output_pipe.name, nullptr);
            server.start(nullptr);
            server.send(L"message");
            Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(wait_entered, 2'000),
                             L"the output worker did not enter WaitNamedPipe");

            const auto start = std::chrono::steady_clock::now();
            server.end();
            const auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::steady_clock::now() - start);

            two_way_pipe_message_ipc_test::SetWaitNamedPipeEnteredEvent(nullptr);
            CloseHandle(wait_entered);
            Assert::IsTrue(elapsed.count() < 1'000,
                           L"end waited too long for an unavailable output pipe");
        }
    };
}
