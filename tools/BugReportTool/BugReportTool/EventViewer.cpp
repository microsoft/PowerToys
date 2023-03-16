#include "EventViewer.h"

#include <windows.h>
#include <sddl.h>
#include <stdio.h>
#include <winevt.h>
#include <fstream>
#include <string>
#include <common/utils/winapi_error.h>

#include "XmlDocumentEx.h"

extern std::vector<std::wstring> processes;

namespace
{
    // Batch size for number of events queried at once
    constexpr int BATCH_SIZE = 50;

    class EventViewerReporter
    {
    private:
        // Report last 30 days
        const long long PERIOD = 10 * 24 * 3600ll * 1000;

        const std::wstring QUERY = L"<QueryList>" \
            L"  <Query Id='0'>" \
            L"    <Select Path='Application'>" \
            L"        *[System[TimeCreated[timediff(@SystemTime)&lt;%I64u]]] " \
            L"        and *[EventData[Data and (Data='%s')]]" \
            L"    </Select>" \
            L"  </Query>" \
            L"</QueryList>";

        std::wstring GetQuery(std::wstring processName)
        {
            wchar_t buff[1000];
            memset(buff, 0, sizeof(buff));
            _snwprintf_s(buff, sizeof(buff), QUERY.c_str(), PERIOD, processName.c_str());
            return buff;
        }

        std::wofstream report;
        EVT_HANDLE hResults;

        void PrintEvent(EVT_HANDLE hEvent)
        {
            DWORD status = ERROR_SUCCESS;
            DWORD dwBufferSize = 0;
            DWORD dwBufferUsed = 0;
            DWORD dwPropertyCount = 0;
            LPWSTR pRenderedContent = NULL;

            // The EvtRenderEventXml flag tells EvtRender to render the event as an XML string.
            if (!EvtRender(NULL, hEvent, EvtRenderEventXml, dwBufferSize, pRenderedContent, &dwBufferUsed, &dwPropertyCount))
            {
                if (ERROR_INSUFFICIENT_BUFFER == (status = GetLastError()))
                {
                    dwBufferSize = dwBufferUsed;
                    pRenderedContent = static_cast<LPWSTR>(malloc(dwBufferSize));
                    if (pRenderedContent)
                    {
                        EvtRender(NULL, hEvent, EvtRenderEventXml, dwBufferSize, pRenderedContent, &dwBufferUsed, &dwPropertyCount);
                    }
                }
                
                if (ERROR_SUCCESS != (status = GetLastError()))
                {
                    report << std::endl << L"EvtRender failed with " << get_last_error_or_default(GetLastError()) << std::endl << std::endl;
                    if (pRenderedContent)
                    {
                        free(pRenderedContent);
                    }
                    return;
                }
            }

            XmlDocumentEx doc;
            doc.LoadXml(pRenderedContent);
            std::wstring formattedXml = L"";
            try
            {
                formattedXml = doc.GetFormatedXml();
            }
            catch (...)
            {
                formattedXml = pRenderedContent;
            }

            report << std::endl << formattedXml << std::endl;
            if (pRenderedContent)
            {
                free(pRenderedContent);
            }
        }

        // Enumerate all the events in the result set. 
        void PrintResults(EVT_HANDLE results)
        {
            DWORD status = ERROR_SUCCESS;
            EVT_HANDLE hEvents[BATCH_SIZE];
            DWORD dwReturned = 0;

            while (true)
            {
                // Get a block of events from the result set.
                if (!EvtNext(results, BATCH_SIZE, hEvents, INFINITE, 0, &dwReturned))
                {
                    if (ERROR_NO_MORE_ITEMS != (status = GetLastError()))
                    {
                        report << L"EvtNext failed with " << status << std::endl;
                    }

                    break;
                }

                // For each event, call the PrintEvent function which renders the
                // event for display. PrintEvent is shown in RenderingEvents.
                for (DWORD i = 0; i < dwReturned; i++)
                {
                    PrintEvent(hEvents[i]);
                }
            }

            for (DWORD i = 0; i < dwReturned; i++)
            {
                if (nullptr != hEvents[i])
                    EvtClose(hEvents[i]);
            }
        }

    public:
        EventViewerReporter(const std::filesystem::path& tmpDir, std::wstring processName)
        {
            auto query = GetQuery(processName);
            auto reportPath = tmpDir;
            reportPath.append(L"EventViewer-" + processName + L".xml");
            report = std::wofstream(reportPath);

            hResults = EvtQuery(NULL, NULL, GetQuery(processName).c_str(), EvtQueryChannelPath);
            if (NULL == hResults)
            {
                report << "Failed to report info for " << processName << ". " << get_last_error_or_default(GetLastError()) << std::endl;
                return;
            }
        }

        ~EventViewerReporter()
        {
            if (hResults)
            {
                EvtClose(hResults);
                hResults = nullptr;
            }
        }

        void Report()
        {
            try
            {
                if (hResults)
                {
                    PrintResults(hResults);
                }
            }
            catch (...)
            {
                report << "Failed to report info" << std::endl;
            }
        }
    };
}

void EventViewer::ReportEventViewerInfo(const std::filesystem::path& tmpDir)
{
    for (auto& process : processes)
    {
        EventViewerReporter(tmpDir, process).Report();
    }
}
