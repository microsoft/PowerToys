#pragma once
#include "../ProtectedStorage.Common/Protocol.h"
#include <mutex>

namespace PowerToys::ProtectedStorage
{
    class BlobStore
    {
    public:
        // Caller must hold validated root handles for the lifetime of this store.
        BlobStore(std::wstring directory, std::function<void(HANDLE)> verifyFile);
        Frame Dispatch(const Frame& request);
        void Recover();
        static Value InitialIndex(std::string epoch, const std::vector<std::string>& targets);

    private:
        Value ReadIndex() const;
        void PublishIndex(const Value& index) const;
        Frame ReadGeneration(const Value& index, const std::string& target) const;
        Frame Inspect(const Frame& request);
        Frame Read(const Frame& request);
        Frame Write(const Frame& request);
        Frame Query(const Frame& request);
        Frame Acknowledge(const Frame& request);
        std::wstring m_directory;
        std::function<void(HANDLE)> m_verifyFile;
        std::mutex m_mutex;
    };
    class TransientBlobStore
    {
    public:
        Frame Dispatch(const Frame& request);

    private:
        struct Receipt
        {
            std::string operation, digest;
            uint64_t sequence = 0;
        };
        struct Entry
        {
            std::string id, operation, digest, target, schema;
            uint64_t expires = 0;
            std::vector<BYTE> bytes;
            uint64_t sequence = 1;
            std::vector<Receipt> receipts;
        };
        void Reclaim();
        std::map<std::string, Entry> m_entries;
        size_t m_size = 0;
    };
}
