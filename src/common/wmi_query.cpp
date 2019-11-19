#include "pch.h"

#include "wmi_query.h"

#include <Wbemidl.h>
#include <comutil.h>

#include <wil/com.h>

#include <xmllite.h>

#include <winrt/Windows.Data.Xml.Dom.h>

struct wmi_connection::impl
{
  wil::com_ptr_t<IWbemServices> _services;
  wil::com_ptr_t<IWbemLocator>  _locator;
  wil::com_ptr_t<IWbemContext>  _context;
};

wil::com_ptr_t<IWbemContext> setup_wmi_context()
{
  auto context = wil::CoCreateInstance<WbemContext, IWbemContext>(CLSCTX_INPROC_SERVER);
  _variant_t option;

  option.vt = VT_BOOL;
  option.boolVal = VARIANT_FALSE;
  context->SetValue(L"IncludeQualifiers", 0, &option);
  option.Clear();

  option.vt = VT_I4;
  option.lVal = 0;
  context->SetValue(L"PathLevel", 0, &option);
  option.Clear();

  option.vt = VT_BOOL;
  option.boolVal = VARIANT_TRUE;
  context->SetValue(L"ExcludeSystemProperties", 0, &option);
  option.Clear();

  option.vt = VT_BOOL;
  option.lVal = VARIANT_FALSE;
  context->SetValue(L"LocalOnly", 0, &option);
  option.Clear();

  return context;
}

wmi_connection::wmi_connection()
  :_impl{new impl{}, &default_delete<impl>}
{
}

wmi_connection wmi_connection::initialize()
{
  wmi_connection con;
  con._impl->_locator = wil::CoCreateInstance<WbemLocator, IWbemLocator>(CLSCTX_INPROC_SERVER);
  con._impl->_context = setup_wmi_context();

  if(FAILED(con._impl->_locator->ConnectServer(
    bstr_t("ROOT\\wmi"),
    nullptr,
    nullptr,
    nullptr,
    0,
    nullptr,
    nullptr,
    &con._impl->_services)))
  {
    throw std::runtime_error("cannot initialize WMI!");
  }

  if(FAILED(CoSetProxyBlanket(
    con._impl->_services.get(),
    RPC_C_AUTHN_WINNT,
    RPC_C_AUTHZ_NONE,
    nullptr,
    RPC_C_AUTHN_LEVEL_CALL,
    RPC_C_IMP_LEVEL_IMPERSONATE,
    nullptr,
    EOAC_NONE
  )))
  {
    throw std::runtime_error("cannot initialize WMI!");
  }
  return con;
}

void wmi_connection::select_all(const wchar_t * statement, std::function<void(std::wstring_view)> callback)
{
  bstr_t query_string{"select * from "};
  query_string += statement;
  wil::com_ptr<IEnumWbemClassObject> enum_class_obj;
  if(FAILED(_impl->_services->ExecQuery(
    bstr_t("WQL"),
    query_string,
    WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY,
    nullptr,
    &enum_class_obj)))
  {
    throw std::runtime_error("WMI query error!");
  }
  wil::com_ptr_t<IWbemClassObject> class_obj;
  ULONG got_next{};
  while(!FAILED(enum_class_obj->Next(WBEM_INFINITE, 1, &class_obj, &got_next)) && got_next)
  {
    auto obj_text_src = wil::CoCreateInstance<WbemObjectTextSrc, IWbemObjectTextSrc>(CLSCTX_INPROC_SERVER);
    bstr_t obj_text;
    if(FAILED(obj_text_src->GetText(0,
      class_obj.get(),
      WMI_OBJ_TEXT_CIM_DTD_2_0,
      _impl->_context.get(),
      obj_text.GetAddress())))
    {
      throw std::runtime_error("WMI query error!");
    }
    callback({static_cast<const wchar_t *>(obj_text), obj_text.length()});
  }
}

