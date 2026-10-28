#include "pch.h"
#include "WorkspacesRepository.h"

#include <algorithm>
#include <filesystem>
#include <set>
#include <cmath>
#include <workspaces-common/GuidUtils.h>

namespace Workspaces
{
    namespace Storage = PowerToys::ProtectedStorage;
    namespace
    {
        constexpr char Target[] = "workspaces.repository";
        constexpr char Preview[] = "workspaces.preview";
        constexpr char Schema[] = "workspaces.v1";
        constexpr size_t MaximumBytes = 8 * 1024 * 1024;

        void Require(bool condition)
        {
            Storage::Check(condition, Storage::ErrorCode::InvalidPayload);
        }
        void Text(const std::wstring& text, size_t maximum, bool empty = true)
        {
            Require(text.size() <= maximum && (empty || text.find_first_not_of(L" \t\r\n") != std::wstring::npos));
            Require(std::none_of(text.begin(), text.end(), [](wchar_t c) { return c < 32 || (c >= 127 && c <= 159); }));
        }
        void Rectangle(int x, int y, int width, int height)
        {
            Require(width > 0 && height > 0 && width <= 1000000 && height <= 1000000);
            Require(static_cast<int64_t>(x) + width <= INT_MAX && static_cast<int64_t>(y) + height <= INT_MAX);
        }
        std::wstring Identity(const std::wstring& text)
        {
            const auto value = text.size() == 38 && text.front() == L'{' && text.back() == L'}' ? text.substr(1, 36) : text;
            Require(Storage::ValidId(value));
            const auto guid = GuidFromString(L"{" + value + L"}");
            Require(guid.has_value() && guid.value() != GUID_NULL);
            return GuidToString(*guid);
        }
        std::vector<BYTE> Bytes(const json::JsonObject& value)
        {
            const auto utf8 = Storage::Utf8(value.Stringify().c_str());
            Require(utf8.size() <= MaximumBytes);
            return {utf8.begin(), utf8.end()};
        }
        void Keys(const json::JsonObject& object, std::initializer_list<const wchar_t*> allowed)
        {
            for (const auto& property : object)
            {
                Require(std::any_of(allowed.begin(), allowed.end(), [&](const auto* name) { return property.Key() == name; }));
            }
        }
        void ProjectShape(const json::JsonObject& object)
        {
            Keys(object, {L"id", L"name", L"creation-time", L"last-launched-time", L"is-shortcut-needed",
                L"move-existing-windows", L"applications", L"monitor-configuration"});
            for (const auto& value : object.GetNamedArray(L"applications"))
            {
                const auto app = value.GetObjectW();
                Keys(app, {L"id", L"application", L"application-path", L"title", L"package-full-name",
                    L"app-user-model-id", L"pwa-app-id", L"command-line-arguments", L"is-elevated",
                    L"can-launch-elevated", L"minimized", L"maximized", L"position", L"monitor", L"version"});
                Keys(app.GetNamedObject(L"position"), {L"X", L"Y", L"width", L"height"});
            }
            for (const auto& value : object.GetNamedArray(L"monitor-configuration"))
            {
                const auto monitor = value.GetObjectW();
                Keys(monitor, {L"id", L"instance-id", L"monitor-number", L"dpi", L"monitor-rect-dpi-aware", L"monitor-rect-dpi-unaware"});
                Keys(monitor.GetNamedObject(L"monitor-rect-dpi-aware"), {L"top", L"left", L"width", L"height"});
                Keys(monitor.GetNamedObject(L"monitor-rect-dpi-unaware"), {L"top", L"left", L"width", L"height"});
            }
        }
        void CheckNumbers(const json::IJsonValue& value, const std::wstring& key = {}, unsigned depth = 0)
        {
            Require(depth <= 32);
            switch (value.ValueType())
            {
            case json::JsonValueType::Number:
            {
                const auto number = value.GetNumber();
                const bool timestamp = key == L"creation-time" || key == L"last-launched-time";
                Require(std::isfinite(number) && std::floor(number) == number &&
                    number >= (timestamp ? 0.0 : static_cast<double>(INT_MIN)) &&
                    number <= (timestamp ? 253402300799.0 : static_cast<double>(INT_MAX)));
                break;
            }
            case json::JsonValueType::Object:
                for (const auto& property : value.GetObjectW())
                {
                    CheckNumbers(property.Value(), property.Key().c_str(), depth + 1);
                }
                break;
            case json::JsonValueType::Array:
                Require(value.GetArray().Size() <= 4096);
                for (const auto& item : value.GetArray()) CheckNumbers(item, {}, depth + 1);
                break;
            default: break;
            }
        }
        class UniqueProperties
        {
        public:
            explicit UniqueProperties(std::wstring_view text) : text(text) {}
            void Validate()
            {
                Check();
                Space();
                Require(offset == text.size());
            }
            void Check(unsigned depth = 0)
            {
                Require(depth <= 32);
                Space();
                Require(offset < text.size());
                const wchar_t token = text[offset];
                if (token == L'{')
                {
                    ++offset;
                    Space();
                    std::set<std::wstring> keys;
                    Require(offset < text.size());
                    if (text[offset] == L'}') { ++offset; return; }
                    for (;;)
                    {
                        Space();
                        auto key = json::JsonValue::Parse(String()).GetString();
                        std::wstring canonical(key);
                        for (auto& c : canonical)
                        {
                            Require(c >= 32 && c < 127);
                            if (c >= L'A' && c <= L'Z') c += L'a' - L'A';
                        }
                        Require(keys.size() < 64 && keys.insert(canonical).second);
                        Space();
                        Require(offset < text.size());
                        Require(text[offset++] == L':');
                        Check(depth + 1);
                        Space();
                        Require(offset < text.size());
                        const auto delimiter = text[offset++];
                        if (delimiter == L'}') return;
                        Require(delimiter == L',');
                    }
                }
                if (token == L'[')
                {
                    ++offset;
                    Space();
                    Require(offset < text.size());
                    if (text[offset] == L']') { ++offset; return; }
                    unsigned count = 0;
                    for (;;)
                    {
                        Require(++count <= 4096);
                        Check(depth + 1);
                        Space();
                        Require(offset < text.size());
                        const auto delimiter = text[offset++];
                        if (delimiter == L']') return;
                        Require(delimiter == L',');
                    }
                }
                if (token == L'"') { String(); return; }
                const auto start = offset;
                while (offset < text.size() && text[offset] != L',' && text[offset] != L'}' &&
                    text[offset] != L']' && !iswspace(text[offset])) ++offset;
                if (token == L'-' || (token >= L'0' && token <= L'9'))
                {
                    for (size_t index = start + (token == L'-' ? 1 : 0); index < offset; ++index)
                    {
                        Require(text[index] >= L'0' && text[index] <= L'9');
                    }
                }
            }

        private:
            std::wstring_view text;
            size_t offset = 0;
            void Space() { while (offset < text.size() && iswspace(text[offset])) ++offset; }
            std::wstring String()
            {
                const auto start = offset;
                Require(offset < text.size() && text[offset++] == L'"');
                while (offset < text.size())
                {
                    const auto c = text[offset++];
                    if (c == L'"') return std::wstring(text.substr(start, offset - start));
                    if (c == L'\\') ++offset;
                }
                throw Storage::StorageError(Storage::ErrorCode::InvalidPayload);
            }
        };
        json::JsonObject Parse(const std::vector<BYTE>& bytes)
        {
            Require(!bytes.empty() && bytes.size() <= MaximumBytes);
            const auto text = Storage::Wide(std::string_view(reinterpret_cast<const char*>(bytes.data()), bytes.size()));
            UniqueProperties(text).Validate();
            auto value = json::JsonValue::Parse(text);
            CheckNumbers(value);
            return value.GetObjectW();
        }
    }

    bool Repository::SameIdentity(const std::wstring& first, const std::wstring& second)
    {
        return Identity(first) == Identity(second);
    }

    void Repository::Validate(const std::vector<WorkspacesData::WorkspacesProject>& projects, bool preview)
    {
        Require(projects.size() <= 1024 && (!preview || projects.size() == 1));
        std::set<std::wstring> ids;
        for (const auto& project : projects)
        {
            Require(ids.insert(Identity(project.id)).second);
            Text(project.name, 256, preview);
            Require(project.name.find_first_of(L"<>:\"/\\|?*") == std::wstring::npos);
            Require(project.creationTime >= 0 && project.creationTime <= 253402300799LL);
            Require(!project.lastLaunchedTime || (*project.lastLaunchedTime >= 0 && *project.lastLaunchedTime <= 253402300799LL));
            Require(project.apps.size() <= 4096 && project.monitors.size() <= 128);
            std::set<unsigned> monitors;
            for (const auto& monitor : project.monitors)
            {
                Text(monitor.id, 32767, false);
                Text(monitor.instanceId, 32767);
                Require(monitor.number > 0 && monitors.insert(monitor.number).second && monitor.dpi >= 48 && monitor.dpi <= 9600);
                const auto& aware = monitor.monitorRectDpiAware;
                const auto& unaware = monitor.monitorRectDpiUnaware;
                Rectangle(aware.left, aware.top, aware.width, aware.height);
                Rectangle(unaware.left, unaware.top, unaware.width, unaware.height);
            }
            std::set<std::wstring> apps;
            for (const auto& app : project.apps)
            {
                Require(apps.insert(Identity(app.id)).second);
                Text(app.name, 32767, false);
                Text(app.path, 32767, false);
                Require(std::filesystem::path(app.path).is_absolute() && !app.path.starts_with(L"\\\\?\\") && !app.path.starts_with(L"\\\\.\\"));
                Text(app.title, 32767);
                Text(app.commandLineArgs, 32767);
                Text(app.packageFullName, 4096);
                Text(app.appUserModelId, 4096);
                Require(std::all_of(app.packageFullName.begin(), app.packageFullName.end(), [](wchar_t c) {
                    return (c >= L'a' && c <= L'z') || (c >= L'A' && c <= L'Z') ||
                        (c >= L'0' && c <= L'9') || c == L'.' || c == L'_' || c == L'-';
                }));
                if (app.appUserModelId.starts_with(L"steam://rungameid/"))
                {
                    const auto game = app.appUserModelId.substr(18);
                    Require(!game.empty() && std::all_of(game.begin(), game.end(), [](wchar_t c) { return c >= L'0' && c <= L'9'; }));
                }
                else
                {
                    Require(std::all_of(app.appUserModelId.begin(), app.appUserModelId.end(), [](wchar_t c) {
                        return (c >= L'a' && c <= L'z') || (c >= L'A' && c <= L'Z') ||
                            (c >= L'0' && c <= L'9') || c == L'.' || c == L'_' || c == L'-' || c == L'!';
                    }));
                }
                Text(app.pwaAppId, 256);
                Text(app.version, 16);
                Require(app.version.empty() || std::all_of(app.version.begin(), app.version.end(), [](wchar_t c) { return c >= L'0' && c <= L'9'; }));
                Require(app.version.empty() || std::stoull(app.version) <= INT_MAX);
                Require(app.pwaAppId.empty() || std::all_of(app.pwaAppId.begin(), app.pwaAppId.end(), [](wchar_t c) { return (c >= L'a' && c <= L'z') || (c >= L'A' && c <= L'Z') || (c >= L'0' && c <= L'9') || c == L'-' || c == L'_'; }));
                Require(monitors.contains(app.monitor));
                Rectangle(app.position.x, app.position.y, app.position.width, app.position.height);
            }
        }
    }

    RepositorySnapshot Repository::Load()
    {
        VerifyReady();
        if (pending)
        {
            const auto result = client.QueryWrite(Target, pending->operationId);
            Storage::Check(result.outcome == "Committed" || result.outcome == "NotCommitted", Storage::ErrorCode::OutcomeUnknown);
            pending.reset();
        }
        auto blob = client.GetBlob(Target);
        Require(blob.contentSchema == Schema);
        return {ParseDocument(blob.bytes), blob.revision};
    }

    std::vector<WorkspacesData::WorkspacesProject> Repository::ParseDocument(const std::vector<BYTE>& bytes)
    {
        const auto object = Parse(bytes);
        Keys(object, {L"workspaces"});
        for (const auto& project : object.GetNamedArray(L"workspaces")) ProjectShape(project.GetObjectW());
        auto projects = WorkspacesData::WorkspacesListJSON::FromJson(object);
        Require(projects.has_value());
        Validate(*projects);
        return *projects;
    }

    WorkspacesData::WorkspacesProject Repository::ParseProject(const std::vector<BYTE>& bytes)
    {
        const auto object = Parse(bytes);
        ProjectShape(object);
        auto project = WorkspacesData::WorkspacesProjectJSON::FromJson(object);
        Require(project.has_value());
        Validate({*project}, true);
        return *project;
    }

    std::vector<BYTE> Repository::SerializeProject(const WorkspacesData::WorkspacesProject& project)
    {
        Validate({project}, true);
        return Bytes(WorkspacesData::WorkspacesProjectJSON::ToJson(project));
    }

    WorkspacesData::WorkspacesProject Repository::ReadPreview(const std::string& id)
    {
        VerifyReady();
        auto blob = client.GetTransientBlob(Preview, id);
        Require(blob.contentSchema == Schema);
        return ParseProject(blob.bytes);
    }

    std::string Repository::CreatePreview(const WorkspacesData::WorkspacesProject& project)
    {
        VerifyReady();
        return client.CreateTransientBlob(Preview, Storage::Utf8(Storage::NewId()), SerializeProject(project), Schema);
    }

    void Repository::VerifyReady()
    {
        const auto capabilities = client.GetCapabilities();
        Storage::Check(capabilities.At("protocolMajor").Number() == 1 && capabilities.At("protocolMinor").Number() == 0 &&
            capabilities.At("maxBlobBytes").Number() >= MaximumBytes && capabilities.At("maxMetadataBytes").Number() >= 8192,
            Storage::ErrorCode::IncompatibleVersion);
        for (const auto* required : { "cas", "write-query", "source-receipt", "transient", "signed-peer-catalog", "purge-suppression" })
        {
            const auto& features = capabilities.At("features").Items();
            Storage::Check(std::any_of(features.begin(), features.end(), [&](const auto& feature) { return feature.Text() == required; }),
                Storage::ErrorCode::IncompatibleVersion);
        }
        Storage::Check(!capabilities.At("maintenance").Boolean(), Storage::ErrorCode::BusyMaintenance);
        Storage::Check(!capabilities.At("recoveryRequired").Boolean(), Storage::ErrorCode::RecoveryRequired);
    }

    void Repository::MergeLaunchMetadata(std::vector<WorkspacesData::WorkspacesProject>& projects,
                                         const WorkspacesData::WorkspacesProject& original,
                                         const WorkspacesData::WorkspacesProject& updated,
                                         std::optional<time_t> launched)
    {
        const auto found = std::find_if(projects.begin(), projects.end(), [&](const auto& project) { return SameIdentity(project.id, original.id); });
        Storage::Check(found != projects.end() && found->creationTime == original.creationTime, Storage::ErrorCode::RevisionConflict);
        for (const auto& before : original.apps)
        {
            const auto after = std::find_if(updated.apps.begin(), updated.apps.end(), [&](const auto& app) { return SameIdentity(app.id, before.id); });
            Require(after != updated.apps.end());
            if (*after == before) continue;
            const auto current = std::find_if(found->apps.begin(), found->apps.end(), [&](const auto& app) { return SameIdentity(app.id, before.id); });
            Storage::Check(current != found->apps.end() && (*current == before || *current == *after), Storage::ErrorCode::RevisionConflict);
            // Only installation-derived launch metadata is merged. User flags, arguments,
            // window positions and unrelated workspaces always come from the latest record.
            current->name = after->name;
            current->path = after->path;
            current->packageFullName = after->packageFullName;
            current->appUserModelId = after->appUserModelId;
            current->pwaAppId = after->pwaAppId;
            current->version = after->version;
        }
        if (launched)
        {
            found->lastLaunchedTime = (std::max)(found->lastLaunchedTime.value_or(0), *launched);
        }
    }

    void Repository::UpdateLaunchMetadata(const WorkspacesData::WorkspacesProject& original,
                                          const WorkspacesData::WorkspacesProject& updated,
                                          std::optional<time_t> launched)
    {
        for (unsigned attempt = 0; attempt < 4; ++attempt)
        {
            auto snapshot = Load();
            MergeLaunchMetadata(snapshot.projects, original, updated, launched);
            Validate(snapshot.projects);
            Storage::WriteRequest request{Storage::Utf8(Storage::NewId()), Target, Storage::WriteConditionKind::IfRevision,
                snapshot.revision, Schema, Bytes(WorkspacesData::WorkspacesListJSON::ToJson(snapshot.projects))};
            try
            {
                pending = request;
                auto result = client.PutBlob(request);
                Storage::Check(result.outcome == "Committed", Storage::ErrorCode::OutcomeUnknown);
                pending.reset();
                return;
            }
            catch (const Storage::StorageError& error)
            {
                if (error.errorCode == Storage::ErrorCode::RevisionConflict)
                {
                    pending.reset();
                    continue;
                }
                if (error.errorCode == Storage::ErrorCode::OutcomeUnknown ||
                    error.errorCode == Storage::ErrorCode::Timeout ||
                    error.errorCode == Storage::ErrorCode::InvalidPayload)
                {
                    const auto result = client.QueryWrite(Target, request.operationId);
                    if (result.outcome == "Committed")
                    {
                        pending.reset();
                        return;
                    }
                    throw Storage::StorageError(Storage::ErrorCode::OutcomeUnknown, error.code, request.operationId);
                }
                pending.reset();
                throw;
            }
        }
        throw Storage::StorageError(Storage::ErrorCode::RevisionConflict);
    }
}
