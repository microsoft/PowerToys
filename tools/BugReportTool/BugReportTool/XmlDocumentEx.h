#pragma once
#include <winrt/Windows.Data.Xml.Dom.h>

class XmlDocumentEx : public winrt::Windows::Data::Xml::Dom::XmlDocument
{
private:
	std::wstringstream stream;
	void Print(winrt::Windows::Data::Xml::Dom::IXmlNode node, int indentation);
	void PrintTagWithAttributes(winrt::Windows::Data::Xml::Dom::IXmlNode node);

public:
	std::wstring GetFormatedXml();
};
