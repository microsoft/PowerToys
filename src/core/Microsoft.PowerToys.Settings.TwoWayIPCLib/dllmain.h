// dllmain.h : Declaration of module class.

class CTwoWayIPCLibModule : public ATL::CAtlDllModuleT< CTwoWayIPCLibModule >
{
public :
	DECLARE_LIBID(LIBID_TwoWayIPCLibLib)
	DECLARE_REGISTRY_APPID_RESOURCEID(IDR_TWOWAYIPCLIB, "{7e00d5a3-11df-45c1-a675-96e408bd5bf5}")
};

extern class CTwoWayIPCLibModule _AtlModule;
