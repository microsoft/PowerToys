#include "pch.h"
#include "TestHelpers.h"
#include <timeutil.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(TimeUtilsTests)
    {
    public:
        // to_string tests
        TEST_METHOD(ToString_ZeroTime_ReturnsZero)
        {
            time_t t = 0;
            auto result = timeutil::to_string(t);
            Assert::AreEqual(std::wstring(L"0"), result);
        }

        TEST_METHOD(ToString_PositiveTime_ReturnsString)
        {
            time_t t = 1234567890;
            auto result = timeutil::to_string(t);
            Assert::AreEqual(std::wstring(L"1234567890"), result);
        }

        TEST_METHOD(ToString_LargeTime_ReturnsString)
        {
            time_t t = 1700000000;
            auto result = timeutil::to_string(t);
            Assert::AreEqual(std::wstring(L"1700000000"), result);
        }

        // from_string tests
        TEST_METHOD(FromString_ZeroString_ReturnsZero)
        {
            auto result = timeutil::from_string(L"0");
            Assert::IsTrue(result.has_value());
            Assert::AreEqual(static_cast<time_t>(0), result.value());
        }

        TEST_METHOD(FromString_ValidNumber_ReturnsTime)
        {
            auto result = timeutil::from_string(L"1234567890");
            Assert::IsTrue(result.has_value());
            Assert::AreEqual(static_cast<time_t>(1234567890), result.value());
        }

        TEST_METHOD(FromString_InvalidString_ReturnsNullopt)
        {
            auto result = timeutil::from_string(L"invalid");
            Assert::IsFalse(result.has_value());
        }

        TEST_METHOD(FromString_EmptyString_ReturnsNullopt)
        {
            auto result = timeutil::from_string(L"");
            Assert::IsFalse(result.has_value());
        }

        TEST_METHOD(FromString_MixedAlphaNumeric_ReturnsNullopt)
        {
            auto result = timeutil::from_string(L"123abc");
            Assert::IsFalse(result.has_value());
        }

        TEST_METHOD(FromString_NegativeNumber_ReturnsNullopt)
        {
            auto result = timeutil::from_string(L"-1");
            Assert::IsFalse(result.has_value());
        }

        // Roundtrip test
        TEST_METHOD(ToStringFromString_Roundtrip_Works)
        {
            time_t original = 1609459200; // 2021-01-01 00:00:00 UTC
            auto str = timeutil::to_string(original);
            auto result = timeutil::from_string(str);
            Assert::IsTrue(result.has_value());
            Assert::AreEqual(original, result.value());
        }

        // now tests
        TEST_METHOD(Now_ReturnsReasonableTime)
        {
            auto result = timeutil::now();
            // Should be after 2020 and before 2100
            Assert::IsTrue(result > 1577836800); // 2020-01-01
            Assert::IsTrue(result < 4102444800); // 2100-01-01
        }

        TEST_METHOD(Now_TwoCallsAreCloseInTime)
        {
            auto first = timeutil::now();
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
            auto second = timeutil::now();
            // Difference should be less than 2 seconds
            Assert::IsTrue(second >= first);
            Assert::IsTrue(second - first < 2);
        }

        // diff::in_seconds tests
        TEST_METHOD(DiffInSeconds_SameTime_ReturnsZero)
        {
            time_t t = 1000000;
            auto result = timeutil::diff::in_seconds(t, t);
            Assert::AreEqual(static_cast<int64_t>(0), result);
        }

        TEST_METHOD(DiffInSeconds_OneDifference_ReturnsOne)
        {
            time_t to = 1000001;
            time_t from = 1000000;
            auto result = timeutil::diff::in_seconds(to, from);
            Assert::AreEqual(static_cast<int64_t>(1), result);
        }

        TEST_METHOD(DiffInSeconds_60Seconds_Returns60)
        {
            time_t to = 1000060;
            time_t from = 1000000;
            auto result = timeutil::diff::in_seconds(to, from);
            Assert::AreEqual(static_cast<int64_t>(60), result);
        }

        TEST_METHOD(DiffInSeconds_NegativeDiff_ReturnsNegative)
        {
            time_t to = 1000000;
            time_t from = 1000060;
            auto result = timeutil::diff::in_seconds(to, from);
            Assert::AreEqual(static_cast<int64_t>(-60), result);
        }

        // diff::in_minutes tests
        TEST_METHOD(DiffInMinutes_SameTime_ReturnsZero)
        {
            time_t t = 1000000;
            auto result = timeutil::diff::in_minutes(t, t);
            Assert::AreEqual(static_cast<int64_t>(0), result);
        }

        TEST_METHOD(DiffInMinutes_OneMinute_ReturnsOne)
        {
            time_t to = 1000060;
            time_t from = 1000000;
            auto result = timeutil::diff::in_minutes(to, from);
            Assert::AreEqual(static_cast<int64_t>(1), result);
        }

        TEST_METHOD(DiffInMinutes_60Minutes_Returns60)
        {
            time_t to = 1003600;
            time_t from = 1000000;
            auto result = timeutil::diff::in_minutes(to, from);
            Assert::AreEqual(static_cast<int64_t>(60), result);
        }

        TEST_METHOD(DiffInMinutes_LessThanMinute_ReturnsZero)
        {
            time_t to = 1000059;
            time_t from = 1000000;
            auto result = timeutil::diff::in_minutes(to, from);
            Assert::AreEqual(static_cast<int64_t>(0), result);
        }

        // diff::in_hours tests
        TEST_METHOD(DiffInHours_SameTime_ReturnsZero)
        {
            time_t t = 1000000;
            auto result = timeutil::diff::in_hours(t, t);
            Assert::AreEqual(static_cast<int64_t>(0), result);
        }

        TEST_METHOD(DiffInHours_OneHour_ReturnsOne)
        {
            time_t to = 1003600;
            time_t from = 1000000;
            auto result = timeutil::diff::in_hours(to, from);
            Assert::AreEqual(static_cast<int64_t>(1), result);
        }

        TEST_METHOD(DiffInHours_24Hours_Returns24)
        {
            time_t to = 1086400;
            time_t from = 1000000;
            auto result = timeutil::diff::in_hours(to, from);
            Assert::AreEqual(static_cast<int64_t>(24), result);
        }

        TEST_METHOD(DiffInHours_LessThanHour_ReturnsZero)
        {
            time_t to = 1003599;
            time_t from = 1000000;
            auto result = timeutil::diff::in_hours(to, from);
            Assert::AreEqual(static_cast<int64_t>(0), result);
        }

        // diff::in_days tests
        TEST_METHOD(DiffInDays_SameTime_ReturnsZero)
        {
            time_t t = 1000000;
            auto result = timeutil::diff::in_days(t, t);
            Assert::AreEqual(static_cast<int64_t>(0), result);
        }

        TEST_METHOD(DiffInDays_OneDay_ReturnsOne)
        {
            time_t to = 1086400;
            time_t from = 1000000;
            auto result = timeutil::diff::in_days(to, from);
            Assert::AreEqual(static_cast<int64_t>(1), result);
        }

        TEST_METHOD(DiffInDays_7Days_Returns7)
        {
            time_t to = 1604800;
            time_t from = 1000000;
            auto result = timeutil::diff::in_days(to, from);
            Assert::AreEqual(static_cast<int64_t>(7), result);
        }

        TEST_METHOD(DiffInDays_LessThanDay_ReturnsZero)
        {
            time_t to = 1086399;
            time_t from = 1000000;
            auto result = timeutil::diff::in_days(to, from);
            Assert::AreEqual(static_cast<int64_t>(0), result);
        }

        // format_as_local tests
        TEST_METHOD(FormatAsLocal_YearFormat_ReturnsYear)
        {
            time_t t = 1609459200; // 2021-01-01 00:00:00 UTC
            auto result = timeutil::format_as_local("%Y", t);
            // Result depends on local timezone, but year should be 2020 or 2021
            Assert::IsTrue(result == "2020" || result == "2021");
        }

        TEST_METHOD(FormatAsLocal_DateFormat_ReturnsDate)
        {
            time_t t = 0; // 1970-01-01 00:00:00 UTC
            auto result = timeutil::format_as_local("%Y-%m-%d", t);
            // Result should be a date around 1970-01-01 depending on timezone
            Assert::IsTrue(result.length() == 10); // YYYY-MM-DD format
            Assert::IsTrue(result.substr(0, 4) == "1969" || result.substr(0, 4) == "1970");
        }
    };
}
