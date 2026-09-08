using System;
using System.Reflection;
using Shikaku.Notifications;
using NUnit.Framework;
using Unity.Notifications;
using Unity.Notifications.iOS;

namespace Shikaku.EditorTests
{
    public sealed class NotificationReminderServiceTests
    {
        [Test]
        public void IOSPermissionIsNotRequestedAutomatically()
        {
            Assert.That(
                NotificationSettings.iOSSettings
                    .RequestAuthorizationOnAppLaunch,
                Is.False);
            Assert.That(
                NotificationSettings.iOSSettings
                    .DefaultAuthorizationOptions,
                Is.EqualTo(AuthorizationOption.Alert));
        }

        [Test]
        public void ScheduleContainsAtMostTwoSixPmReminders()
        {
            DateTime now = new DateTime(
                2026,
                8,
                24,
                9,
                30,
                0,
                DateTimeKind.Local);

            DateTime[] fireTimes = BuildFireTimes(
                now,
                now.ToUniversalTime().AddHours(66),
                completedToday: false);

            Assert.That(fireTimes, Has.Length.EqualTo(2));
            Assert.That(fireTimes[0].Date, Is.EqualTo(now.Date));
            Assert.That(fireTimes[0].Hour, Is.EqualTo(18));
            Assert.That(fireTimes[0].Minute, Is.Zero);
            Assert.That(
                fireTimes[1].Date,
                Is.EqualTo(now.Date.AddDays(1)));
        }

        [Test]
        public void CompletedDailyPuzzleSkipsTodaysReminder()
        {
            DateTime now = new DateTime(
                2026,
                8,
                24,
                9,
                30,
                0,
                DateTimeKind.Local);

            DateTime[] fireTimes = BuildFireTimes(
                now,
                now.ToUniversalTime().AddHours(66),
                completedToday: true);

            Assert.That(fireTimes, Is.Not.Empty);
            Assert.That(
                fireTimes[0].Date,
                Is.EqualTo(now.Date.AddDays(1)));
        }

        [Test]
        public void ScheduleDoesNotCrossInactivityCutoff()
        {
            DateTime now = new DateTime(
                2026,
                8,
                24,
                17,
                30,
                0,
                DateTimeKind.Local);
            DateTime cutoffUtc =
                now.ToUniversalTime().AddMinutes(45);

            DateTime[] fireTimes = BuildFireTimes(
                now,
                cutoffUtc,
                completedToday: false);

            Assert.That(fireTimes, Has.Length.EqualTo(1));
            Assert.That(
                fireTimes[0].ToUniversalTime(),
                Is.LessThanOrEqualTo(cutoffUtc));
        }

        private static DateTime[] BuildFireTimes(
            DateTime now,
            DateTime cutoffUtc,
            bool completedToday)
        {
            MethodInfo method =
                typeof(NotificationReminderService).GetMethod(
                    "BuildReminderFireTimes",
                    BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null);
            return (DateTime[])method.Invoke(
                null,
                new object[] { now, cutoffUtc, completedToday });
        }
    }
}
