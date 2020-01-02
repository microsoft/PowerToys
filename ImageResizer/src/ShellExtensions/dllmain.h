class CShellExtensionsModule : public ATL::CAtlDllModuleT< CShellExtensionsModule >
{
public:
	DECLARE_LIBID(LIBID_ShellExtensionsLib)
	DECLARE_REGISTRY_APPID_RESOURCEID(IDR_SHELLEXTENSIONS, "{0C866E7B-65CB-4E7D-B1DD-D014F000E8D8}")
};

extern class CShellExtensionsModule _AtlModule;
