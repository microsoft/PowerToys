#include "pch.h"
#include "WmiHelpers.h"

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Data.Xml.Dom.h>
#include <xmllite.h>

// Dtd provides some utf16 strings as a <VALUE>-wrapped arrays of char codes. This function parses it to a wstring
std::wstring parse_utf16_value_array(const winrt::Windows::Data::Xml::Dom::IXmlNode & value_array_node)
{
  std::wstring result;
  if(!value_array_node)
  {
    throw std::invalid_argument("can't parse an empty xml node!");
  }
  for(auto char_node = value_array_node.FirstChild(); char_node; char_node = char_node.NextSibling())
  {
    uint16_t char_code{};
    std::wistringstream{std::wstring{char_node.InnerText()}} >> char_code;
    if(char_code)
    {
      result += char_code;
    }
    else
    {
      return result;
    }
  }
  return result;
}

// Parse dtd object representation returned from WMI into C++-friendly struct
std::optional<WmiMonitorID> parse_monitorID_from_dtd(std::wstring_view xml)
{
  if(xml.empty())
  {
    return std::nullopt;
  }
  try
  {
    winrt::Windows::Data::Xml::Dom::XmlDocument xml_doc;
    winrt::Windows::Data::Xml::Dom::XmlLoadSettings load_settings;
    load_settings.ValidateOnParse(false);
    load_settings.ElementContentWhiteSpace(false);
    xml_doc.LoadXml(xml, load_settings);
    xml_doc.Normalize();

    WmiMonitorID result;

    for(const auto & node : xml_doc.GetElementsByTagName(L"PROPERTY"))
    {
      for(const auto & attr : node.Attributes())
      {
        if(attr.NodeName() != L"NAME")
        {
          continue;
        }
        const auto property_name = attr.InnerText();

        if(property_name == L"InstanceName")
        {
          result._instance_name = node.FirstChild().InnerText();
        }
      }
    }

    for(const auto & node : xml_doc.GetElementsByTagName(L"PROPERTY.ARRAY"))
    {
      for(const auto & attr : node.Attributes())
      {
        if(attr.NodeName() != L"NAME")
        {
          continue;
        }
        const auto property_name = attr.InnerText();

        if(property_name == L"UserFriendlyName")
        {
          result._friendly_name = parse_utf16_value_array(node.FirstChild());
        }
        else if(property_name == L"SerialNumberID")
        {
          result._serial_number_id = parse_utf16_value_array(node.FirstChild());
        }
      }
    }
    return result;
  }
  catch(...)
  {
    return std::nullopt;
  }
}
