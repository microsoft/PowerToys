#pragma once
#include "pch.h"

// Wrapper around SRWLOCK
class CSRWLock
{
public:
    CSRWLock()
    {
        InitializeSRWLock(&m_lock);
    }

    _Acquires_shared_lock_(this->m_lock)
    void LockShared()
    {
        AcquireSRWLockShared(&m_lock);
    }

    _Acquires_exclusive_lock_(this->m_lock)
    void LockExclusive()
    {
        AcquireSRWLockExclusive(&m_lock);
    }

    _Releases_shared_lock_(this->m_lock)
    void ReleaseShared()
    {
        ReleaseSRWLockShared(&m_lock);
    }

    _Releases_exclusive_lock_(this->m_lock)
    void ReleaseExclusive()
    {
        ReleaseSRWLockExclusive(&m_lock);
    }

    virtual ~CSRWLock()
    {
    }

private:
    SRWLOCK m_lock;
};

// RAII over an SRWLock (write)
class CSRWExclusiveAutoLock
{
public:
    CSRWExclusiveAutoLock(CSRWLock *srwLock)
    {
        m_pSRWLock = srwLock;
        srwLock->LockExclusive();
    }

    virtual ~CSRWExclusiveAutoLock()
    {
        m_pSRWLock->ReleaseExclusive();
    }
protected:
    CSRWLock *m_pSRWLock;
};

// RAII over an SRWLock (read)
class CSRWSharedAutoLock
{
public:
    CSRWSharedAutoLock(CSRWLock *srwLock)
    {
        m_pSRWLock = srwLock;
        srwLock->LockShared();
    }

    virtual ~CSRWSharedAutoLock()
    {
        m_pSRWLock->ReleaseShared();
    }
protected:
    CSRWLock *m_pSRWLock;
};