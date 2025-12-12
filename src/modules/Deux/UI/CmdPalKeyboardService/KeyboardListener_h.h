/* Header file automatically generated from KeyboardListener.idl */
/*
 * File built with Microsoft(R) MIDLRT Compiler Engine Version 10.00.0231 
 */

#pragma warning( disable: 4049 )  /* more than 64k source lines */

/* verify that the <rpcndr.h> version is high enough to compile this file*/
#ifndef __REQUIRED_RPCNDR_H_VERSION__
#define __REQUIRED_RPCNDR_H_VERSION__ 500
#endif

/* verify that the <rpcsal.h> version is high enough to compile this file*/
#ifndef __REQUIRED_RPCSAL_H_VERSION__
#define __REQUIRED_RPCSAL_H_VERSION__ 100
#endif

#include <rpc.h>
#include <rpcndr.h>

#ifndef __RPCNDR_H_VERSION__
#error this stub requires an updated version of <rpcndr.h>
#endif /* __RPCNDR_H_VERSION__ */

#ifndef COM_NO_WINDOWS_H
#include <windows.h>
#include <ole2.h>
#endif /*COM_NO_WINDOWS_H*/
#ifndef __KeyboardListener_h_h__
#define __KeyboardListener_h_h__
#ifndef __KeyboardListener_h_p_h__
#define __KeyboardListener_h_p_h__


#pragma once

// Ensure that the setting of the /ns_prefix command line switch is consistent for all headers.
// If you get an error from the compiler indicating "warning C4005: 'CHECK_NS_PREFIX_STATE': macro redefinition", this
// indicates that you have included two different headers with different settings for the /ns_prefix MIDL command line switch
#if !defined(DISABLE_NS_PREFIX_CHECKS)
#define CHECK_NS_PREFIX_STATE "always"
#endif // !defined(DISABLE_NS_PREFIX_CHECKS)


#pragma push_macro("MIDL_CONST_ID")
#undef MIDL_CONST_ID
#define MIDL_CONST_ID const __declspec(selectany)


// Header files for imported files
#include "winrtbase.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.AI.MachineLearning.MachineLearningContract\5.0.0.0\Windows.AI.MachineLearning.MachineLearningContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.AI.MachineLearning.Preview.MachineLearningPreviewContract\2.0.0.0\Windows.AI.MachineLearning.Preview.MachineLearningPreviewContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.ApplicationModel.Calls.Background.CallsBackgroundContract\4.0.0.0\Windows.ApplicationModel.Calls.Background.CallsBackgroundContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.ApplicationModel.Calls.CallsPhoneContract\7.0.0.0\Windows.ApplicationModel.Calls.CallsPhoneContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.ApplicationModel.Calls.CallsVoipContract\5.0.0.0\Windows.ApplicationModel.Calls.CallsVoipContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.ApplicationModel.CommunicationBlocking.CommunicationBlockingContract\2.0.0.0\Windows.ApplicationModel.CommunicationBlocking.CommunicationBlockingContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.ApplicationModel.SocialInfo.SocialInfoContract\2.0.0.0\Windows.ApplicationModel.SocialInfo.SocialInfoContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.ApplicationModel.StartupTaskContract\3.0.0.0\Windows.ApplicationModel.StartupTaskContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.Devices.Custom.CustomDeviceContract\1.0.0.0\Windows.Devices.Custom.CustomDeviceContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.Devices.DevicesLowLevelContract\3.0.0.0\Windows.Devices.DevicesLowLevelContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.Devices.Printers.PrintersContract\1.0.0.0\Windows.Devices.Printers.PrintersContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.Devices.SmartCards.SmartCardBackgroundTriggerContract\3.0.0.0\Windows.Devices.SmartCards.SmartCardBackgroundTriggerContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.Devices.SmartCards.SmartCardEmulatorContract\6.0.0.0\Windows.Devices.SmartCards.SmartCardEmulatorContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.Foundation.FoundationContract\4.0.0.0\Windows.Foundation.FoundationContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.Foundation.UniversalApiContract\19.0.0.0\Windows.Foundation.UniversalApiContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.Gaming.XboxLive.StorageApiContract\1.0.0.0\Windows.Gaming.XboxLive.StorageApiContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.Graphics.Printing3D.Printing3DContract\4.0.0.0\Windows.Graphics.Printing3D.Printing3DContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.Networking.Connectivity.WwanContract\3.0.0.0\Windows.Networking.Connectivity.WwanContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.Networking.Sockets.ControlChannelTriggerContract\3.0.0.0\Windows.Networking.Sockets.ControlChannelTriggerContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.Security.Isolation.IsolatedWindowsEnvironmentContract\5.0.0.0\Windows.Security.Isolation.Isolatedwindowsenvironmentcontract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.Services.Maps.GuidanceContract\3.0.0.0\Windows.Services.Maps.GuidanceContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.Services.Maps.LocalSearchContract\4.0.0.0\Windows.Services.Maps.LocalSearchContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.Services.Store.StoreContract\4.0.0.0\Windows.Services.Store.StoreContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.Services.TargetedContent.TargetedContentContract\1.0.0.0\Windows.Services.TargetedContent.TargetedContentContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.Storage.Provider.CloudFilesContract\7.0.0.0\Windows.Storage.Provider.CloudFilesContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.System.Profile.ProfileHardwareTokenContract\1.0.0.0\Windows.System.Profile.ProfileHardwareTokenContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.System.Profile.ProfileRetailInfoContract\1.0.0.0\Windows.System.Profile.ProfileRetailInfoContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.System.Profile.ProfileSharedModeContract\2.0.0.0\Windows.System.Profile.ProfileSharedModeContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.System.Profile.SystemManufacturers.SystemManufacturersContract\3.0.0.0\Windows.System.Profile.SystemManufacturers.SystemManufacturersContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.System.SystemManagementContract\7.0.0.0\Windows.System.SystemManagementContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.UI.UIAutomation.UIAutomationContract\2.0.0.0\Windows.UI.UIAutomation.UIAutomationContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.UI.ViewManagement.ViewManagementViewScalingContract\1.0.0.0\Windows.UI.ViewManagement.ViewManagementViewScalingContract.h"
#include "C:\Program Files (x86)\Windows Kits\10\References\10.0.26100.0\Windows.UI.Xaml.Core.Direct.XamlDirectContract\5.0.0.0\Windows.UI.Xaml.Core.Direct.XamlDirectContract.h"

#if defined(__cplusplus) && !defined(CINTERFACE)
#if defined(__MIDL_USE_C_ENUM)
#define MIDL_ENUM enum
#else
#define MIDL_ENUM enum class
#endif
/* Forward Declarations */
#ifndef ____x_ABI_CCmdPalKeyboardService_CIProcessCommand_FWD_DEFINED__
#define ____x_ABI_CCmdPalKeyboardService_CIProcessCommand_FWD_DEFINED__
namespace ABI {
    namespace CmdPalKeyboardService {
        interface IProcessCommand;
    } /* CmdPalKeyboardService */
} /* ABI */
#define __x_ABI_CCmdPalKeyboardService_CIProcessCommand ABI::CmdPalKeyboardService::IProcessCommand

#endif // ____x_ABI_CCmdPalKeyboardService_CIProcessCommand_FWD_DEFINED__

#ifndef ____x_ABI_CCmdPalKeyboardService_CIKeyboardListener_FWD_DEFINED__
#define ____x_ABI_CCmdPalKeyboardService_CIKeyboardListener_FWD_DEFINED__
namespace ABI {
    namespace CmdPalKeyboardService {
        interface IKeyboardListener;
    } /* CmdPalKeyboardService */
} /* ABI */
#define __x_ABI_CCmdPalKeyboardService_CIKeyboardListener ABI::CmdPalKeyboardService::IKeyboardListener

#endif // ____x_ABI_CCmdPalKeyboardService_CIKeyboardListener_FWD_DEFINED__



/*
 *
 * Delegate CmdPalKeyboardService.ProcessCommand
 *
 */
#if !defined(____x_ABI_CCmdPalKeyboardService_CIProcessCommand_INTERFACE_DEFINED__)
#define ____x_ABI_CCmdPalKeyboardService_CIProcessCommand_INTERFACE_DEFINED__
namespace ABI {
    namespace CmdPalKeyboardService {
        /* [object, uuid("78ab07cd-e128-4e73-86aa-e48e6b6d01ff"), version] */
        MIDL_INTERFACE("78ab07cd-e128-4e73-86aa-e48e6b6d01ff")
        IProcessCommand : public IUnknown
        {
        public:
            virtual HRESULT STDMETHODCALLTYPE Invoke(
                /* [in] */HSTRING id
                ) = 0;
            
        };

        MIDL_CONST_ID IID & IID_IProcessCommand=__uuidof(IProcessCommand);
        
    } /* CmdPalKeyboardService */
} /* ABI */

EXTERN_C const IID IID___x_ABI_CCmdPalKeyboardService_CIProcessCommand;
#endif /* !defined(____x_ABI_CCmdPalKeyboardService_CIProcessCommand_INTERFACE_DEFINED__) */

namespace ABI {
    namespace CmdPalKeyboardService {
        class KeyboardListener;
    } /* CmdPalKeyboardService */
} /* ABI */



/*
 *
 * Interface CmdPalKeyboardService.IKeyboardListener
 *
 * Interface is a part of the implementation of type CmdPalKeyboardService.KeyboardListener
 *
 *
 * The IID for this interface was automatically generated by MIDLRT.
 *
 * Interface IID generation seed: CmdPalKeyboardService.IKeyboardListener:HRESULT Start();HRESULT Stop();HRESULT SetHotkeyAction(Boolean,Boolean,Boolean,Boolean,UInt8,String);HRESULT ClearHotkey(String);HRESULT ClearHotkeys();HRESULT SetProcessCommand(CmdPalKeyboardService.ProcessCommand*);
 *
 *
 */
#if !defined(____x_ABI_CCmdPalKeyboardService_CIKeyboardListener_INTERFACE_DEFINED__)
#define ____x_ABI_CCmdPalKeyboardService_CIKeyboardListener_INTERFACE_DEFINED__
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_CmdPalKeyboardService_IKeyboardListener[] = L"CmdPalKeyboardService.IKeyboardListener";
namespace ABI {
    namespace CmdPalKeyboardService {
        /* [uuid("2ae4bb1c-96bd-5c41-a41b-f25b9523efe9"), version, object, exclusiveto] */
        MIDL_INTERFACE("2ae4bb1c-96bd-5c41-a41b-f25b9523efe9")
        IKeyboardListener : public IInspectable
        {
        public:
            virtual HRESULT STDMETHODCALLTYPE Start(void) = 0;
            virtual HRESULT STDMETHODCALLTYPE Stop(void) = 0;
            virtual HRESULT STDMETHODCALLTYPE SetHotkeyAction(
                /* [in] */::boolean win,
                /* [in] */::boolean ctrl,
                /* [in] */::boolean shift,
                /* [in] */::boolean alt,
                /* [in] */::byte key,
                /* [in] */HSTRING id
                ) = 0;
            virtual HRESULT STDMETHODCALLTYPE ClearHotkey(
                /* [in] */HSTRING id
                ) = 0;
            virtual HRESULT STDMETHODCALLTYPE ClearHotkeys(void) = 0;
            virtual HRESULT STDMETHODCALLTYPE SetProcessCommand(
                /* [in] */ABI::CmdPalKeyboardService::IProcessCommand  * processCommand
                ) = 0;
            
        };

        MIDL_CONST_ID IID & IID_IKeyboardListener=__uuidof(IKeyboardListener);
        
    } /* CmdPalKeyboardService */
} /* ABI */

EXTERN_C const IID IID___x_ABI_CCmdPalKeyboardService_CIKeyboardListener;
#endif /* !defined(____x_ABI_CCmdPalKeyboardService_CIKeyboardListener_INTERFACE_DEFINED__) */


/*
 *
 * Class CmdPalKeyboardService.KeyboardListener
 *
 * RuntimeClass can be activated.
 *
 * Class implements the following interfaces:
 *    CmdPalKeyboardService.IKeyboardListener ** Default Interface **
 *
 * Class Threading Model:  Both Single and Multi Threaded Apartment
 *
 * Class Marshaling Behavior:  Agile - Class is agile
 *
 */

#ifndef RUNTIMECLASS_CmdPalKeyboardService_KeyboardListener_DEFINED
#define RUNTIMECLASS_CmdPalKeyboardService_KeyboardListener_DEFINED
extern const __declspec(selectany) _Null_terminated_ WCHAR RuntimeClass_CmdPalKeyboardService_KeyboardListener[] = L"CmdPalKeyboardService.KeyboardListener";
#endif


#else // !defined(__cplusplus)
/* Forward Declarations */
#ifndef ____x_ABI_CCmdPalKeyboardService_CIProcessCommand_FWD_DEFINED__
#define ____x_ABI_CCmdPalKeyboardService_CIProcessCommand_FWD_DEFINED__
typedef interface __x_ABI_CCmdPalKeyboardService_CIProcessCommand __x_ABI_CCmdPalKeyboardService_CIProcessCommand;

#endif // ____x_ABI_CCmdPalKeyboardService_CIProcessCommand_FWD_DEFINED__

#ifndef ____x_ABI_CCmdPalKeyboardService_CIKeyboardListener_FWD_DEFINED__
#define ____x_ABI_CCmdPalKeyboardService_CIKeyboardListener_FWD_DEFINED__
typedef interface __x_ABI_CCmdPalKeyboardService_CIKeyboardListener __x_ABI_CCmdPalKeyboardService_CIKeyboardListener;

#endif // ____x_ABI_CCmdPalKeyboardService_CIKeyboardListener_FWD_DEFINED__


/*
 *
 * Delegate CmdPalKeyboardService.ProcessCommand
 *
 */
#if !defined(____x_ABI_CCmdPalKeyboardService_CIProcessCommand_INTERFACE_DEFINED__)
#define ____x_ABI_CCmdPalKeyboardService_CIProcessCommand_INTERFACE_DEFINED__
/* [object, uuid("78ab07cd-e128-4e73-86aa-e48e6b6d01ff"), version] */
typedef struct __x_ABI_CCmdPalKeyboardService_CIProcessCommandVtbl
{
    BEGIN_INTERFACE
    HRESULT ( STDMETHODCALLTYPE *QueryInterface )(
    __RPC__in __x_ABI_CCmdPalKeyboardService_CIProcessCommand * This,
    /* [in] */ __RPC__in REFIID riid,
    /* [annotation][iid_is][out] */
    _COM_Outptr_  void **ppvObject);

ULONG ( STDMETHODCALLTYPE *AddRef )(
    __RPC__in __x_ABI_CCmdPalKeyboardService_CIProcessCommand * This);

ULONG ( STDMETHODCALLTYPE *Release )(
    __RPC__in __x_ABI_CCmdPalKeyboardService_CIProcessCommand * This);
HRESULT ( STDMETHODCALLTYPE *Invoke )(
        __x_ABI_CCmdPalKeyboardService_CIProcessCommand * This,
        /* [in] */HSTRING id
        );
    END_INTERFACE
    
} __x_ABI_CCmdPalKeyboardService_CIProcessCommandVtbl;

interface __x_ABI_CCmdPalKeyboardService_CIProcessCommand
{
    CONST_VTBL struct __x_ABI_CCmdPalKeyboardService_CIProcessCommandVtbl *lpVtbl;
};

#ifdef COBJMACROS
#define __x_ABI_CCmdPalKeyboardService_CIProcessCommand_QueryInterface(This,riid,ppvObject) \
( (This)->lpVtbl->QueryInterface(This,riid,ppvObject) )

#define __x_ABI_CCmdPalKeyboardService_CIProcessCommand_AddRef(This) \
        ( (This)->lpVtbl->AddRef(This) )

#define __x_ABI_CCmdPalKeyboardService_CIProcessCommand_Release(This) \
        ( (This)->lpVtbl->Release(This) )

#define __x_ABI_CCmdPalKeyboardService_CIProcessCommand_Invoke(This,id) \
    ( (This)->lpVtbl->Invoke(This,id) )


#endif /* COBJMACROS */


EXTERN_C const IID IID___x_ABI_CCmdPalKeyboardService_CIProcessCommand;
#endif /* !defined(____x_ABI_CCmdPalKeyboardService_CIProcessCommand_INTERFACE_DEFINED__) */



/*
 *
 * Interface CmdPalKeyboardService.IKeyboardListener
 *
 * Interface is a part of the implementation of type CmdPalKeyboardService.KeyboardListener
 *
 *
 * The IID for this interface was automatically generated by MIDLRT.
 *
 * Interface IID generation seed: CmdPalKeyboardService.IKeyboardListener:HRESULT Start();HRESULT Stop();HRESULT SetHotkeyAction(Boolean,Boolean,Boolean,Boolean,UInt8,String);HRESULT ClearHotkey(String);HRESULT ClearHotkeys();HRESULT SetProcessCommand(CmdPalKeyboardService.ProcessCommand*);
 *
 *
 */
#if !defined(____x_ABI_CCmdPalKeyboardService_CIKeyboardListener_INTERFACE_DEFINED__)
#define ____x_ABI_CCmdPalKeyboardService_CIKeyboardListener_INTERFACE_DEFINED__
extern const __declspec(selectany) _Null_terminated_ WCHAR InterfaceName_CmdPalKeyboardService_IKeyboardListener[] = L"CmdPalKeyboardService.IKeyboardListener";
/* [uuid("2ae4bb1c-96bd-5c41-a41b-f25b9523efe9"), version, object, exclusiveto] */
typedef struct __x_ABI_CCmdPalKeyboardService_CIKeyboardListenerVtbl
{
    BEGIN_INTERFACE
    HRESULT ( STDMETHODCALLTYPE *QueryInterface)(
    __RPC__in __x_ABI_CCmdPalKeyboardService_CIKeyboardListener * This,
    /* [in] */ __RPC__in REFIID riid,
    /* [annotation][iid_is][out] */
    _COM_Outptr_  void **ppvObject
    );

ULONG ( STDMETHODCALLTYPE *AddRef )(
    __RPC__in __x_ABI_CCmdPalKeyboardService_CIKeyboardListener * This
    );

ULONG ( STDMETHODCALLTYPE *Release )(
    __RPC__in __x_ABI_CCmdPalKeyboardService_CIKeyboardListener * This
    );

HRESULT ( STDMETHODCALLTYPE *GetIids )(
    __RPC__in __x_ABI_CCmdPalKeyboardService_CIKeyboardListener * This,
    /* [out] */ __RPC__out ULONG *iidCount,
    /* [size_is][size_is][out] */ __RPC__deref_out_ecount_full_opt(*iidCount) IID **iids
    );

HRESULT ( STDMETHODCALLTYPE *GetRuntimeClassName )(
    __RPC__in __x_ABI_CCmdPalKeyboardService_CIKeyboardListener * This,
    /* [out] */ __RPC__deref_out_opt HSTRING *className
    );

HRESULT ( STDMETHODCALLTYPE *GetTrustLevel )(
    __RPC__in __x_ABI_CCmdPalKeyboardService_CIKeyboardListener * This,
    /* [OUT ] */ __RPC__out TrustLevel *trustLevel
    );
HRESULT ( STDMETHODCALLTYPE *Start )(
        __x_ABI_CCmdPalKeyboardService_CIKeyboardListener * This
        );
    HRESULT ( STDMETHODCALLTYPE *Stop )(
        __x_ABI_CCmdPalKeyboardService_CIKeyboardListener * This
        );
    HRESULT ( STDMETHODCALLTYPE *SetHotkeyAction )(
        __x_ABI_CCmdPalKeyboardService_CIKeyboardListener * This,
        /* [in] */boolean win,
        /* [in] */boolean ctrl,
        /* [in] */boolean shift,
        /* [in] */boolean alt,
        /* [in] */byte key,
        /* [in] */HSTRING id
        );
    HRESULT ( STDMETHODCALLTYPE *ClearHotkey )(
        __x_ABI_CCmdPalKeyboardService_CIKeyboardListener * This,
        /* [in] */HSTRING id
        );
    HRESULT ( STDMETHODCALLTYPE *ClearHotkeys )(
        __x_ABI_CCmdPalKeyboardService_CIKeyboardListener * This
        );
    HRESULT ( STDMETHODCALLTYPE *SetProcessCommand )(
        __x_ABI_CCmdPalKeyboardService_CIKeyboardListener * This,
        /* [in] */__x_ABI_CCmdPalKeyboardService_CIProcessCommand  * processCommand
        );
    END_INTERFACE
    
} __x_ABI_CCmdPalKeyboardService_CIKeyboardListenerVtbl;

interface __x_ABI_CCmdPalKeyboardService_CIKeyboardListener
{
    CONST_VTBL struct __x_ABI_CCmdPalKeyboardService_CIKeyboardListenerVtbl *lpVtbl;
};

#ifdef COBJMACROS
#define __x_ABI_CCmdPalKeyboardService_CIKeyboardListener_QueryInterface(This,riid,ppvObject) \
( (This)->lpVtbl->QueryInterface(This,riid,ppvObject) )

#define __x_ABI_CCmdPalKeyboardService_CIKeyboardListener_AddRef(This) \
        ( (This)->lpVtbl->AddRef(This) )

#define __x_ABI_CCmdPalKeyboardService_CIKeyboardListener_Release(This) \
        ( (This)->lpVtbl->Release(This) )

#define __x_ABI_CCmdPalKeyboardService_CIKeyboardListener_GetIids(This,iidCount,iids) \
        ( (This)->lpVtbl->GetIids(This,iidCount,iids) )

#define __x_ABI_CCmdPalKeyboardService_CIKeyboardListener_GetRuntimeClassName(This,className) \
        ( (This)->lpVtbl->GetRuntimeClassName(This,className) )

#define __x_ABI_CCmdPalKeyboardService_CIKeyboardListener_GetTrustLevel(This,trustLevel) \
        ( (This)->lpVtbl->GetTrustLevel(This,trustLevel) )

#define __x_ABI_CCmdPalKeyboardService_CIKeyboardListener_Start(This) \
    ( (This)->lpVtbl->Start(This) )

#define __x_ABI_CCmdPalKeyboardService_CIKeyboardListener_Stop(This) \
    ( (This)->lpVtbl->Stop(This) )

#define __x_ABI_CCmdPalKeyboardService_CIKeyboardListener_SetHotkeyAction(This,win,ctrl,shift,alt,key,id) \
    ( (This)->lpVtbl->SetHotkeyAction(This,win,ctrl,shift,alt,key,id) )

#define __x_ABI_CCmdPalKeyboardService_CIKeyboardListener_ClearHotkey(This,id) \
    ( (This)->lpVtbl->ClearHotkey(This,id) )

#define __x_ABI_CCmdPalKeyboardService_CIKeyboardListener_ClearHotkeys(This) \
    ( (This)->lpVtbl->ClearHotkeys(This) )

#define __x_ABI_CCmdPalKeyboardService_CIKeyboardListener_SetProcessCommand(This,processCommand) \
    ( (This)->lpVtbl->SetProcessCommand(This,processCommand) )


#endif /* COBJMACROS */


EXTERN_C const IID IID___x_ABI_CCmdPalKeyboardService_CIKeyboardListener;
#endif /* !defined(____x_ABI_CCmdPalKeyboardService_CIKeyboardListener_INTERFACE_DEFINED__) */


/*
 *
 * Class CmdPalKeyboardService.KeyboardListener
 *
 * RuntimeClass can be activated.
 *
 * Class implements the following interfaces:
 *    CmdPalKeyboardService.IKeyboardListener ** Default Interface **
 *
 * Class Threading Model:  Both Single and Multi Threaded Apartment
 *
 * Class Marshaling Behavior:  Agile - Class is agile
 *
 */

#ifndef RUNTIMECLASS_CmdPalKeyboardService_KeyboardListener_DEFINED
#define RUNTIMECLASS_CmdPalKeyboardService_KeyboardListener_DEFINED
extern const __declspec(selectany) _Null_terminated_ WCHAR RuntimeClass_CmdPalKeyboardService_KeyboardListener[] = L"CmdPalKeyboardService.KeyboardListener";
#endif


#endif // defined(__cplusplus)
#pragma pop_macro("MIDL_CONST_ID")
#endif // __KeyboardListener_h_p_h__

#endif // __KeyboardListener_h_h__
