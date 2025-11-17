# PowerToy ModuleTemplate

# Introduction
This project is used to generate the Visual Studio PowerToys Module Template

# Instruction
In Visual Studio from the menu Project->Export Template... generate the template.
Set the name `PowerToys Module`, add a description `A project for creating a PowerToys module` and an icon.
Open the resulting .zip file in `%USERNAME%\Documents\Visual Studio 2022\Templates\ProjectTemplates`
and edit `MyTemplate.vstemplate` to make the necessary changes, the resulting template should look like this:

```xml
<VSTemplate Version="3.0.0" xmlns="http://schemas.microsoft.com/developer/vstemplate/2005" Type="Project">
  <TemplateData>
    <Name>PowerToys Module</Name>
    <Description>A project for creating a PowerToys module</Description>
    <ProjectType>VC</ProjectType>
    <ProjectSubType>
    </ProjectSubType>
	  <LanguageTag>C++</LanguageTag>
	  <PlatformTag>windows</PlatformTag>
	  <ProjectTypeTag>extension</ProjectTypeTag>
    <SortOrder>1000</SortOrder>
    <CreateNewFolder>true</CreateNewFolder>
    <DefaultName>PowerToy</DefaultName>
    <ProvideDefaultName>true</ProvideDefaultName>
    <LocationField>Enabled</LocationField>
    <EnableLocationBrowseButton>true</EnableLocationBrowseButton>
    <Icon>__TemplateIcon.ico</Icon>
  </TemplateData>
  <TemplateContent>
    <Project TargetFileName="$projectname$.vcxproj" File="ModuleTemplate.vcxproj" ReplaceParameters="true">
      <ProjectItem ReplaceParameters="false" TargetFileName="$projectname$.vcxproj.filters">ModuleTemplate.vcxproj.filters</ProjectItem>
      <ProjectItem ReplaceParameters="true" TargetFileName="dllmain.cpp">dllmain.cpp</ProjectItem>
      <ProjectItem ReplaceParameters="false" TargetFileName="pch.cpp">pch.cpp</ProjectItem>
      <ProjectItem ReplaceParameters="false" TargetFileName="trace.cpp">trace.cpp</ProjectItem>
      <ProjectItem ReplaceParameters="false" TargetFileName="pch.h">pch.h</ProjectItem>
      <ProjectItem ReplaceParameters="false" TargetFileName="resource.h">resource.h</ProjectItem>
      <ProjectItem ReplaceParameters="false" TargetFileName="trace.h">trace.h</ProjectItem>
      <ProjectItem ReplaceParameters="true" TargetFileName="$projectname$.rc">ModuleTemplate.rc</ProjectItem>
    </Project>
  </TemplateContent>
</VSTemplate>
```