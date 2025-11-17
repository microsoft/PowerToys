#pragma once

typedef wil::unique_any<HTHUMBNAIL, decltype(&::DwmUnregisterThumbnail), ::DwmUnregisterThumbnail> unique_hthumbnail;