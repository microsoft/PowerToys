#include "XmlDocumentEx.h"

#include <winrt/Windows.Foundation.Collections.h>

std::wstring XmlDocumentEx::GetFormatedXml()
{
    stream.clear();
    Print(FirstChild(), 0);
    return stream.str();
}

void XmlDocumentEx::Print(winrt::Windows::Data::Xml::Dom::IXmlNode node, int indentation)
{
    for (int i = 0; i < indentation; i++)
    {
        stream << " ";
    }

    PrintTagWithAttributes(node);
    if (!node.HasChildNodes())
    {
        stream << L"<\\" << node.NodeName().c_str() << ">" << std::endl;
        return;
    }

    if (node.ChildNodes().Size() == 1 && !node.FirstChild().HasChildNodes())
    {
        stream << node.InnerText().c_str() << L"<\\" << node.NodeName().c_str() << ">" << std::endl;
        return;
    }

    stream << std::endl;
    auto child = node.FirstChild();
    do
    {
        Print(child, indentation + 2);
    } while (child = child.NextSibling());

    for (int i = 0; i < indentation; i++)
    {
        stream << " ";
    }
    stream << L"<\\" << node.NodeName().c_str() << ">" << std::endl;
}

void XmlDocumentEx::PrintTagWithAttributes(winrt::Windows::Data::Xml::Dom::IXmlNode node)
{
    stream << L"<" << node.NodeName().c_str();
    for (int i = 0; i < (int)node.Attributes().Size(); i++)
    {
        auto attr = node.Attributes().GetAt(i);
        stream << L" " << attr.NodeName().c_str() << L"='" << attr.InnerText().c_str() << L"'";
    }

    stream << L">";
}
