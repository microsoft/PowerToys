#include "pch.h"
#include "TestHelpers.h"
#include <com_object_factory.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    // Test COM object for testing the factory
    class TestComObject : public IUnknown
    {
    public:
        TestComObject() : m_refCount(1) {}

        HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override
        {
            if (riid == IID_IUnknown)
            {
                *ppvObject = static_cast<IUnknown*>(this);
                AddRef();
                return S_OK;
            }
            *ppvObject = nullptr;
            return E_NOINTERFACE;
        }

        ULONG STDMETHODCALLTYPE AddRef() override
        {
            return InterlockedIncrement(&m_refCount);
        }

        ULONG STDMETHODCALLTYPE Release() override
        {
            ULONG count = InterlockedDecrement(&m_refCount);
            if (count == 0)
            {
                delete this;
            }
            return count;
        }

    private:
        LONG m_refCount;
    };

    TEST_CLASS(ComObjectFactoryTests)
    {
    public:
        TEST_METHOD(ComObjectFactory_Construction_DoesNotCrash)
        {
            com_object_factory<TestComObject> factory;
            Assert::IsTrue(true);
        }

        TEST_METHOD(ComObjectFactory_QueryInterface_IUnknown_Succeeds)
        {
            com_object_factory<TestComObject> factory;
            IUnknown* pUnknown = nullptr;

            HRESULT hr = factory.QueryInterface(IID_IUnknown, reinterpret_cast<void**>(&pUnknown));

            Assert::AreEqual(S_OK, hr);
            Assert::IsNotNull(pUnknown);

            if (pUnknown)
            {
                pUnknown->Release();
            }
        }

        TEST_METHOD(ComObjectFactory_QueryInterface_IClassFactory_Succeeds)
        {
            com_object_factory<TestComObject> factory;
            IClassFactory* pFactory = nullptr;

            HRESULT hr = factory.QueryInterface(IID_IClassFactory, reinterpret_cast<void**>(&pFactory));

            Assert::AreEqual(S_OK, hr);
            Assert::IsNotNull(pFactory);

            if (pFactory)
            {
                pFactory->Release();
            }
        }

        TEST_METHOD(ComObjectFactory_QueryInterface_InvalidInterface_Fails)
        {
            com_object_factory<TestComObject> factory;
            void* pInterface = nullptr;

            // Random GUID that we don't support
            GUID randomGuid = { 0x12345678, 0x1234, 0x1234, { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 } };
            HRESULT hr = factory.QueryInterface(randomGuid, &pInterface);

            Assert::AreEqual(E_NOINTERFACE, hr);
            Assert::IsNull(pInterface);
        }

        TEST_METHOD(ComObjectFactory_AddRef_IncreasesRefCount)
        {
            com_object_factory<TestComObject> factory;

            ULONG count1 = factory.AddRef();
            ULONG count2 = factory.AddRef();

            Assert::IsTrue(count2 > count1);

            // Clean up
            factory.Release();
            factory.Release();
        }

        TEST_METHOD(ComObjectFactory_Release_DecreasesRefCount)
        {
            com_object_factory<TestComObject> factory;

            factory.AddRef();
            factory.AddRef();
            ULONG count1 = factory.Release();
            ULONG count2 = factory.Release();

            Assert::IsTrue(count2 < count1);
        }

        TEST_METHOD(ComObjectFactory_CreateInstance_NoAggregation_Succeeds)
        {
            com_object_factory<TestComObject> factory;
            IUnknown* pObj = nullptr;

            HRESULT hr = factory.CreateInstance(nullptr, IID_IUnknown, reinterpret_cast<void**>(&pObj));

            Assert::AreEqual(S_OK, hr);
            Assert::IsNotNull(pObj);

            if (pObj)
            {
                pObj->Release();
            }
        }

        TEST_METHOD(ComObjectFactory_CreateInstance_WithAggregation_Fails)
        {
            com_object_factory<TestComObject> factory;
            TestComObject outer;
            IUnknown* pObj = nullptr;

            // Aggregation should fail for our simple test object
            HRESULT hr = factory.CreateInstance(&outer, IID_IUnknown, reinterpret_cast<void**>(&pObj));

            Assert::AreEqual(CLASS_E_NOAGGREGATION, hr);
            Assert::IsNull(pObj);
        }

        TEST_METHOD(ComObjectFactory_CreateInstance_NullOutput_Fails)
        {
            com_object_factory<TestComObject> factory;

            HRESULT hr = factory.CreateInstance(nullptr, IID_IUnknown, nullptr);

            Assert::AreEqual(E_POINTER, hr);
        }

        TEST_METHOD(ComObjectFactory_LockServer_Lock_Succeeds)
        {
            com_object_factory<TestComObject> factory;

            HRESULT hr = factory.LockServer(TRUE);
            Assert::AreEqual(S_OK, hr);

            // Unlock
            factory.LockServer(FALSE);
        }

        TEST_METHOD(ComObjectFactory_LockServer_Unlock_Succeeds)
        {
            com_object_factory<TestComObject> factory;

            factory.LockServer(TRUE);
            HRESULT hr = factory.LockServer(FALSE);

            Assert::AreEqual(S_OK, hr);
        }

        TEST_METHOD(ComObjectFactory_LockServer_MultipleLocks_Work)
        {
            com_object_factory<TestComObject> factory;

            factory.LockServer(TRUE);
            factory.LockServer(TRUE);
            factory.LockServer(TRUE);

            factory.LockServer(FALSE);
            factory.LockServer(FALSE);
            HRESULT hr = factory.LockServer(FALSE);

            Assert::AreEqual(S_OK, hr);
        }

        // Thread safety tests
        TEST_METHOD(ComObjectFactory_ConcurrentCreateInstance_Works)
        {
            com_object_factory<TestComObject> factory;
            std::vector<std::thread> threads;
            std::atomic<int> successCount{ 0 };

            for (int i = 0; i < 10; ++i)
            {
                threads.emplace_back([&factory, &successCount]() {
                    IUnknown* pObj = nullptr;
                    HRESULT hr = factory.CreateInstance(nullptr, IID_IUnknown, reinterpret_cast<void**>(&pObj));
                    if (SUCCEEDED(hr) && pObj)
                    {
                        successCount++;
                        pObj->Release();
                    }
                });
            }

            for (auto& t : threads)
            {
                t.join();
            }

            Assert::AreEqual(10, successCount.load());
        }
    };
}
