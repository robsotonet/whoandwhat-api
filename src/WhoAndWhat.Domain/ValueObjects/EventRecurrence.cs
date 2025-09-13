namespace WhoAndWhat.Domain.ValueObjects;

/// <summary>
/// Value object representing event recurrence patterns
/// </summary>
public sealed record EventRecurrence
{
    private EventRecurrence(
        RecurrenceFrequency frequency,
        int interval,
        List<DayOfWeek> daysOfWeek,
        int? dayOfMonth,
        int? weekOfMonth,
        int? monthOfYear,
        DateTime? endDate,
        int? occurrenceCount,
        List<DateTime> exceptionDates,
        RecurrenceEndType endType)
    {
        Frequency = frequency;
        Interval = interval;
        DaysOfWeek = daysOfWeek;
        DayOfMonth = dayOfMonth;
        WeekOfMonth = weekOfMonth;
        MonthOfYear = monthOfYear;
        EndDate = endDate;
        OccurrenceCount = occurrenceCount;
        ExceptionDates = exceptionDates;
        EndType = endType;
    }

    /// <summary>
    /// Frequency of recurrence (daily, weekly, monthly, yearly)
    /// </summary>
    public RecurrenceFrequency Frequency { get; }

    /// <summary>
    /// Interval between recurrences (e.g., every 2 weeks)
    /// </summary>
    public int Interval { get; }

    /// <summary>
    /// Days of the week for weekly recurrence
    /// </summary>
    public List<DayOfWeek> DaysOfWeek { get; }

    /// <summary>
    /// Day of the month for monthly recurrence (1-31)
    /// </summary>
    public int? DayOfMonth { get; }

    /// <summary>
    /// Week of the month for monthly recurrence (1-5, -1 for last week)
    /// </summary>
    public int? WeekOfMonth { get; }

    /// <summary>
    /// Month of the year for yearly recurrence (1-12)
    /// </summary>
    public int? MonthOfYear { get; }

    /// <summary>
    /// End date for the recurrence
    /// </summary>
    public DateTime? EndDate { get; }

    /// <summary>
    /// Maximum number of occurrences
    /// </summary>
    public int? OccurrenceCount { get; }

    /// <summary>
    /// Dates to exclude from the recurrence
    /// </summary>
    public List<DateTime> ExceptionDates { get; }

    /// <summary>
    /// How the recurrence ends
    /// </summary>
    public RecurrenceEndType EndType { get; }

    /// <summary>
    /// Gets whether this recurrence has an end condition
    /// </summary>
    public bool HasEndCondition => EndType != RecurrenceEndType.Never;

    /// <summary>
    /// Gets whether this recurrence is infinite
    /// </summary>
    public bool IsInfinite => EndType == RecurrenceEndType.Never;

    /// <summary>
    /// Gets whether this recurrence has exception dates
    /// </summary>
    public bool HasExceptions => ExceptionDates.Any();

    /// <summary>
    /// Gets a human-readable description of the recurrence pattern
    /// </summary>
    public string Description => GenerateDescription();

    /// <summary>
    /// Creates a daily recurrence pattern
    /// </summary>
    public static EventRecurrence Daily(int interval = 1, DateTime? endDate = null, int? occurrenceCount = null)
    {
        return new EventRecurrence(
            RecurrenceFrequency.Daily,
            interval,
            new List<DayOfWeek>(),
            null,
            null,
            null,
            endDate,
            occurrenceCount,
            new List<DateTime>(),
            DetermineEndType(endDate, occurrenceCount)
        );
    }

    /// <summary>
    /// Creates a weekly recurrence pattern
    /// </summary>
    public static EventRecurrence Weekly(int interval = 1, List<DayOfWeek>? daysOfWeek = null, 
        DateTime? endDate = null, int? occurrenceCount = null)
    {
        return new EventRecurrence(
            RecurrenceFrequency.Weekly,
            interval,
            daysOfWeek ?? new List<DayOfWeek>(),
            null,
            null,
            null,
            endDate,
            occurrenceCount,
            new List<DateTime>(),
            DetermineEndType(endDate, occurrenceCount)
        );
    }

    /// <summary>
    /// Creates a monthly recurrence pattern by day of month
    /// </summary>
    public static EventRecurrence MonthlyByDate(int dayOfMonth, int interval = 1, 
        DateTime? endDate = null, int? occurrenceCount = null)
    {
        if (dayOfMonth < 1 || dayOfMonth > 31)
            throw new ArgumentException("Day of month must be between 1 and 31", nameof(dayOfMonth));

        return new EventRecurrence(
            RecurrenceFrequency.Monthly,
            interval,
            new List<DayOfWeek>(),
            dayOfMonth,
            null,
            null,
            endDate,
            occurrenceCount,
            new List<DateTime>(),
            DetermineEndType(endDate, occurrenceCount)
        );
    }

    /// <summary>
    /// Creates a monthly recurrence pattern by day of week
    /// </summary>
    public static EventRecurrence MonthlyByDayOfWeek(int weekOfMonth, DayOfWeek dayOfWeek, int interval = 1,
        DateTime? endDate = null, int? occurrenceCount = null)
    {
        if (weekOfMonth < -1 || weekOfMonth == 0 || weekOfMonth > 5)
            throw new ArgumentException("Week of month must be between 1-5 or -1 for last week", nameof(weekOfMonth));

        return new EventRecurrence(
            RecurrenceFrequency.Monthly,
            interval,
            new List<DayOfWeek> { dayOfWeek },
            null,
            weekOfMonth,
            null,
            endDate,
            occurrenceCount,
            new List<DateTime>(),
            DetermineEndType(endDate, occurrenceCount)
        );
    }

    /// <summary>
    /// Creates a yearly recurrence pattern
    /// </summary>
    public static EventRecurrence Yearly(int monthOfYear, int dayOfMonth, int interval = 1,
        DateTime? endDate = null, int? occurrenceCount = null)
    {
        if (monthOfYear < 1 || monthOfYear > 12)
            throw new ArgumentException("Month of year must be between 1 and 12", nameof(monthOfYear));
        if (dayOfMonth < 1 || dayOfMonth > 31)
            throw new ArgumentException("Day of month must be between 1 and 31", nameof(dayOfMonth));

        return new EventRecurrence(
            RecurrenceFrequency.Yearly,
            interval,
            new List<DayOfWeek>(),
            dayOfMonth,
            null,
            monthOfYear,
            endDate,
            occurrenceCount,
            new List<DateTime>(),
            DetermineEndType(endDate, occurrenceCount)
        );
    }

    /// <summary>
    /// Creates a yearly recurrence pattern by day of week
    /// </summary>
    public static EventRecurrence YearlyByDayOfWeek(int monthOfYear, int weekOfMonth, DayOfWeek dayOfWeek, 
        int interval = 1, DateTime? endDate = null, int? occurrenceCount = null)
    {
        if (monthOfYear < 1 || monthOfYear > 12)
            throw new ArgumentException("Month of year must be between 1 and 12", nameof(monthOfYear));
        if (weekOfMonth < -1 || weekOfMonth == 0 || weekOfMonth > 5)
            throw new ArgumentException("Week of month must be between 1-5 or -1 for last week", nameof(weekOfMonth));

        return new EventRecurrence(
            RecurrenceFrequency.Yearly,
            interval,
            new List<DayOfWeek> { dayOfWeek },
            null,
            weekOfMonth,
            monthOfYear,
            endDate,
            occurrenceCount,
            new List<DateTime>(),
            DetermineEndType(endDate, occurrenceCount)
        );
    }

    /// <summary>
    /// Adds exception dates to the recurrence pattern
    /// </summary>
    public EventRecurrence WithExceptions(List<DateTime> exceptionDates)
    {
        var exceptions = ExceptionDates.Concat(exceptionDates).Distinct().OrderBy(d => d).ToList();
        
        return new EventRecurrence(
            Frequency,
            Interval,
            DaysOfWeek,
            DayOfMonth,
            WeekOfMonth,
            MonthOfYear,
            EndDate,
            OccurrenceCount,
            exceptions,
            EndType
        );
    }

    /// <summary>
    /// Adds a single exception date to the recurrence pattern
    /// </summary>
    public EventRecurrence WithException(DateTime exceptionDate)
    {
        return WithExceptions(new List<DateTime> { exceptionDate });
    }

    /// <summary>
    /// Generates the next occurrence of the event from a given date
    /// </summary>
    public DateTime? GetNextOccurrence(DateTime fromDate, DateTime eventStartTime)
    {
        var candidate = CalculateNextCandidate(fromDate, eventStartTime);
        
        while (candidate.HasValue)
        {
            // Check if candidate is past end conditions
            if (IsOccurrencePastEnd(candidate.Value))
                return null;

            // Check if candidate is in exception dates
            if (!ExceptionDates.Any(ex => ex.Date == candidate.Value.Date))
                return candidate;

            // Try next candidate
            candidate = CalculateNextCandidate(candidate.Value.AddDays(1), eventStartTime);
        }

        return null;
    }

    /// <summary>
    /// Generates all occurrences within a date range
    /// </summary>
    public List<DateTime> GetOccurrencesInRange(DateTime startRange, DateTime endRange, DateTime eventStartTime, int maxOccurrences = 1000)
    {
        var occurrences = new List<DateTime>();
        var current = eventStartTime >= startRange ? eventStartTime : GetNextOccurrence(startRange, eventStartTime);
        var count = 0;

        while (current.HasValue && current.Value <= endRange && count < maxOccurrences)
        {
            if (!IsOccurrencePastEnd(current.Value))
            {
                occurrences.Add(current.Value);
                count++;
            }

            current = GetNextOccurrence(current.Value.AddDays(1), eventStartTime);
        }

        return occurrences;
    }

    /// <summary>
    /// Validates that a specific date would be an occurrence according to this pattern
    /// </summary>
    public bool IsOccurrenceDate(DateTime date, DateTime eventStartTime)
    {
        // Check if it's an exception date
        if (ExceptionDates.Any(ex => ex.Date == date.Date))
            return false;

        // Check if it's past end conditions
        if (IsOccurrencePastEnd(date))
            return false;

        // Check if it matches the pattern
        return DoesDateMatchPattern(date, eventStartTime);
    }

    /// <summary>
    /// Converts the recurrence to RFC 5545 RRULE format
    /// </summary>
    public string ToRRule()
    {
        var parts = new List<string>();

        // Frequency
        parts.Add($"FREQ={Frequency.ToString().ToUpper()}");

        // Interval
        if (Interval > 1)
            parts.Add($"INTERVAL={Interval}");

        // Days of week for weekly/monthly patterns
        if (DaysOfWeek.Any())
        {
            var days = DaysOfWeek.Select(d => d.ToString().Substring(0, 2).ToUpper());
            parts.Add($"BYDAY={string.Join(",", days)}");
        }

        // Day of month for monthly/yearly patterns
        if (DayOfMonth.HasValue)
            parts.Add($"BYMONTHDAY={DayOfMonth}");

        // Week of month for monthly patterns
        if (WeekOfMonth.HasValue && DaysOfWeek.Any())
        {
            var day = DaysOfWeek.First().ToString().Substring(0, 2).ToUpper();
            parts.Add($"BYDAY={WeekOfMonth}{day}");
        }

        // Month of year for yearly patterns
        if (MonthOfYear.HasValue)
            parts.Add($"BYMONTH={MonthOfYear}");

        // End conditions
        switch (EndType)
        {
            case RecurrenceEndType.Date when EndDate.HasValue:
                parts.Add($"UNTIL={EndDate.Value:yyyyMMddTHHmmssZ}");
                break;
            case RecurrenceEndType.Count when OccurrenceCount.HasValue:
                parts.Add($"COUNT={OccurrenceCount}");
                break;
        }

        return string.Join(";", parts);
    }

    /// <summary>
    /// Creates an EventRecurrence from RFC 5545 RRULE format
    /// </summary>
    public static EventRecurrence FromRRule(string rrule)
    {
        var parts = rrule.Split(';');
        var rules = parts
            .Select(part => part.Split('='))
            .Where(keyValue => keyValue.Length >= 2)
            .ToDictionary(
                keyValue => keyValue[0],
                keyValue => keyValue[1],
                StringComparer.OrdinalIgnoreCase
            );

        var frequency = Enum.Parse<RecurrenceFrequency>(rules["FREQ"], true);
        var interval = rules.ContainsKey("INTERVAL") ? int.Parse(rules["INTERVAL"]) : 1;

        var daysOfWeek = new List<DayOfWeek>();
        if (rules.ContainsKey("BYDAY"))
        {
            var dayStrings = rules["BYDAY"].Split(',');
            foreach (var dayString in dayStrings)
            {
                var cleanDay = dayString.TrimStart('-', '1', '2', '3', '4', '5');
                if (TryParseDayAbbreviation(cleanDay, out var day))
                    daysOfWeek.Add(day);
            }
        }

        int? dayOfMonth = rules.ContainsKey("BYMONTHDAY") ? int.Parse(rules["BYMONTHDAY"]) : null;
        int? monthOfYear = rules.ContainsKey("BYMONTH") ? int.Parse(rules["BYMONTH"]) : null;
        
        int? weekOfMonth = null;
        if (rules.ContainsKey("BYDAY") && rules["BYDAY"].Any(char.IsDigit))
        {
            var byDay = rules["BYDAY"];
            if (byDay.StartsWith("-"))
                weekOfMonth = -1;
            else if (char.IsDigit(byDay[0]))
                weekOfMonth = int.Parse(byDay[0].ToString());
        }

        DateTime? endDate = null;
        int? occurrenceCount = null;
        var endType = RecurrenceEndType.Never;

        if (rules.ContainsKey("UNTIL"))
        {
            endDate = DateTime.ParseExact(rules["UNTIL"], "yyyyMMddTHHmmssZ", null);
            endType = RecurrenceEndType.Date;
        }
        else if (rules.ContainsKey("COUNT"))
        {
            occurrenceCount = int.Parse(rules["COUNT"]);
            endType = RecurrenceEndType.Count;
        }

        return new EventRecurrence(
            frequency,
            interval,
            daysOfWeek,
            dayOfMonth,
            weekOfMonth,
            monthOfYear,
            endDate,
            occurrenceCount,
            new List<DateTime>(),
            endType
        );
    }

    private static RecurrenceEndType DetermineEndType(DateTime? endDate, int? occurrenceCount)
    {
        if (endDate.HasValue) return RecurrenceEndType.Date;
        if (occurrenceCount.HasValue) return RecurrenceEndType.Count;
        return RecurrenceEndType.Never;
    }

    private DateTime? CalculateNextCandidate(DateTime fromDate, DateTime eventStartTime)
    {
        return Frequency switch
        {
            RecurrenceFrequency.Daily => CalculateNextDaily(fromDate, eventStartTime),
            RecurrenceFrequency.Weekly => CalculateNextWeekly(fromDate, eventStartTime),
            RecurrenceFrequency.Monthly => CalculateNextMonthly(fromDate, eventStartTime),
            RecurrenceFrequency.Yearly => CalculateNextYearly(fromDate, eventStartTime),
            _ => null
        };
    }

    private DateTime? CalculateNextDaily(DateTime fromDate, DateTime eventStartTime)
    {
        var daysSinceStart = (fromDate.Date - eventStartTime.Date).Days;
        var intervalsSinceStart = daysSinceStart / Interval;
        var nextInterval = intervalsSinceStart + 1;
        
        return eventStartTime.Date.AddDays(nextInterval * Interval).Add(eventStartTime.TimeOfDay);
    }

    private DateTime? CalculateNextWeekly(DateTime fromDate, DateTime eventStartTime)
    {
        // Use effective days - if none specified, default to the event start day
        var effectiveDays = DaysOfWeek.Any() ? DaysOfWeek : new List<DayOfWeek> { eventStartTime.DayOfWeek };

        var weeksSinceStart = (fromDate.Date - eventStartTime.Date).Days / 7 / Interval;
        var baseWeek = eventStartTime.Date.AddDays(weeksSinceStart * Interval * 7);

        // Find next occurrence in current interval
        for (int i = 0; i < 7; i++)
        {
            var candidate = baseWeek.AddDays(i);
            if (candidate >= fromDate.Date && effectiveDays.Contains(candidate.DayOfWeek))
            {
                return candidate.Add(eventStartTime.TimeOfDay);
            }
        }

        // Try next interval
        baseWeek = baseWeek.AddDays(Interval * 7);
        var firstDayInWeek = DaysOfWeek.Min();
        var daysToAdd = (int)firstDayInWeek - (int)baseWeek.DayOfWeek;
        if (daysToAdd < 0) daysToAdd += 7;

        return baseWeek.AddDays(daysToAdd).Add(eventStartTime.TimeOfDay);
    }

    private DateTime? CalculateNextMonthly(DateTime fromDate, DateTime eventStartTime)
    {
        if (DayOfMonth.HasValue)
        {
            return CalculateNextMonthlyByDate(fromDate, eventStartTime);
        }
        else if (WeekOfMonth.HasValue && DaysOfWeek.Any())
        {
            return CalculateNextMonthlyByDayOfWeek(fromDate, eventStartTime);
        }

        return null;
    }

    private DateTime? CalculateNextMonthlyByDate(DateTime fromDate, DateTime eventStartTime)
    {
        var monthsSinceStart = ((fromDate.Year - eventStartTime.Year) * 12 + fromDate.Month - eventStartTime.Month) / Interval;
        var baseMonth = eventStartTime.AddMonths(monthsSinceStart * Interval);

        // Try current interval
        var daysInMonth = DateTime.DaysInMonth(baseMonth.Year, baseMonth.Month);
        var targetDay = Math.Min(DayOfMonth!.Value, daysInMonth);
        var candidate = new DateTime(baseMonth.Year, baseMonth.Month, targetDay, eventStartTime.Hour, eventStartTime.Minute, eventStartTime.Second);

        if (candidate >= fromDate)
            return candidate;

        // Try next interval
        baseMonth = baseMonth.AddMonths(Interval);
        daysInMonth = DateTime.DaysInMonth(baseMonth.Year, baseMonth.Month);
        targetDay = Math.Min(DayOfMonth.Value, daysInMonth);
        
        return new DateTime(baseMonth.Year, baseMonth.Month, targetDay, eventStartTime.Hour, eventStartTime.Minute, eventStartTime.Second);
    }

    private DateTime? CalculateNextMonthlyByDayOfWeek(DateTime fromDate, DateTime eventStartTime)
    {
        var targetDayOfWeek = DaysOfWeek.First();
        var monthsSinceStart = ((fromDate.Year - eventStartTime.Year) * 12 + fromDate.Month - eventStartTime.Month) / Interval;
        var baseMonth = eventStartTime.AddMonths(monthsSinceStart * Interval);

        // Try current interval
        var candidate = FindDayOfWeekInMonth(baseMonth.Year, baseMonth.Month, WeekOfMonth!.Value, targetDayOfWeek, eventStartTime.TimeOfDay);
        if (candidate >= fromDate)
            return candidate;

        // Try next interval
        baseMonth = baseMonth.AddMonths(Interval);
        return FindDayOfWeekInMonth(baseMonth.Year, baseMonth.Month, WeekOfMonth.Value, targetDayOfWeek, eventStartTime.TimeOfDay);
    }

    private DateTime? CalculateNextYearly(DateTime fromDate, DateTime eventStartTime)
    {
        var yearsSinceStart = (fromDate.Year - eventStartTime.Year) / Interval;
        var baseYear = eventStartTime.Year + yearsSinceStart * Interval;

        DateTime candidate;
        if (DayOfMonth.HasValue)
        {
            var month = MonthOfYear ?? eventStartTime.Month;
            var daysInMonth = DateTime.DaysInMonth(baseYear, month);
            var day = Math.Min(DayOfMonth.Value, daysInMonth);
            candidate = new DateTime(baseYear, month, day, eventStartTime.Hour, eventStartTime.Minute, eventStartTime.Second);
        }
        else if (WeekOfMonth.HasValue && DaysOfWeek.Any())
        {
            var month = MonthOfYear ?? eventStartTime.Month;
            candidate = FindDayOfWeekInMonth(baseYear, month, WeekOfMonth.Value, DaysOfWeek.First(), eventStartTime.TimeOfDay) ?? DateTime.MinValue;
        }
        else
        {
            return null;
        }

        if (candidate >= fromDate)
            return candidate;

        // Try next interval
        baseYear += Interval;
        if (DayOfMonth.HasValue)
        {
            var month = MonthOfYear ?? eventStartTime.Month;
            var daysInMonth = DateTime.DaysInMonth(baseYear, month);
            var day = Math.Min(DayOfMonth.Value, daysInMonth);
            return new DateTime(baseYear, month, day, eventStartTime.Hour, eventStartTime.Minute, eventStartTime.Second);
        }
        else if (WeekOfMonth.HasValue && DaysOfWeek.Any())
        {
            var month = MonthOfYear ?? eventStartTime.Month;
            return FindDayOfWeekInMonth(baseYear, month, WeekOfMonth.Value, DaysOfWeek.First(), eventStartTime.TimeOfDay);
        }

        return null;
    }

    private static DateTime? FindDayOfWeekInMonth(int year, int month, int weekOfMonth, DayOfWeek dayOfWeek, TimeSpan timeOfDay)
    {
        if (weekOfMonth == -1)
        {
            // Last occurrence of the day in the month
            var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            while (lastDay.DayOfWeek != dayOfWeek)
                lastDay = lastDay.AddDays(-1);
            return lastDay.Add(timeOfDay);
        }

        // Find the first occurrence of the day in the month
        var firstDay = new DateTime(year, month, 1);
        while (firstDay.DayOfWeek != dayOfWeek)
            firstDay = firstDay.AddDays(1);

        // Add weeks to get to the desired occurrence
        var targetDate = firstDay.AddDays((weekOfMonth - 1) * 7);

        // Make sure we're still in the same month
        if (targetDate.Month != month)
            return null;

        return targetDate.Add(timeOfDay);
    }

    private bool IsOccurrencePastEnd(DateTime occurrence)
    {
        return EndType switch
        {
            RecurrenceEndType.Date => EndDate.HasValue && occurrence > EndDate.Value,
            RecurrenceEndType.Count => false, // Count checking is handled separately
            _ => false
        };
    }

    private bool DoesDateMatchPattern(DateTime date, DateTime eventStartTime)
    {
        return Frequency switch
        {
            RecurrenceFrequency.Daily => DoesDateMatchDaily(date, eventStartTime),
            RecurrenceFrequency.Weekly => DoesDateMatchWeekly(date, eventStartTime),
            RecurrenceFrequency.Monthly => DoesDateMatchMonthly(date, eventStartTime),
            RecurrenceFrequency.Yearly => DoesDateMatchYearly(date, eventStartTime),
            _ => false
        };
    }

    private bool DoesDateMatchDaily(DateTime date, DateTime eventStartTime)
    {
        var daysDifference = (date.Date - eventStartTime.Date).Days;
        return daysDifference >= 0 && daysDifference % Interval == 0;
    }

    private bool DoesDateMatchWeekly(DateTime date, DateTime eventStartTime)
    {
        if (!DaysOfWeek.Contains(date.DayOfWeek))
            return false;

        var daysDifference = (date.Date - eventStartTime.Date).Days;
        var weeksDifference = daysDifference / 7;
        return weeksDifference >= 0 && weeksDifference % Interval == 0;
    }

    private bool DoesDateMatchMonthly(DateTime date, DateTime eventStartTime)
    {
        var monthsDifference = (date.Year - eventStartTime.Year) * 12 + date.Month - eventStartTime.Month;
        if (monthsDifference < 0 || monthsDifference % Interval != 0)
            return false;

        if (DayOfMonth.HasValue)
        {
            return date.Day == DayOfMonth.Value;
        }
        else if (WeekOfMonth.HasValue && DaysOfWeek.Any())
        {
            var targetDate = FindDayOfWeekInMonth(date.Year, date.Month, WeekOfMonth.Value, DaysOfWeek.First(), TimeSpan.Zero);
            return targetDate?.Date == date.Date;
        }

        return false;
    }

    private bool DoesDateMatchYearly(DateTime date, DateTime eventStartTime)
    {
        var yearsDifference = date.Year - eventStartTime.Year;
        if (yearsDifference < 0 || yearsDifference % Interval != 0)
            return false;

        var targetMonth = MonthOfYear ?? eventStartTime.Month;
        if (date.Month != targetMonth)
            return false;

        if (DayOfMonth.HasValue)
        {
            return date.Day == DayOfMonth.Value;
        }
        else if (WeekOfMonth.HasValue && DaysOfWeek.Any())
        {
            var targetDate = FindDayOfWeekInMonth(date.Year, date.Month, WeekOfMonth.Value, DaysOfWeek.First(), TimeSpan.Zero);
            return targetDate?.Date == date.Date;
        }

        return false;
    }

    private string GenerateDescription()
    {
        var parts = new List<string>();

        switch (Frequency)
        {
            case RecurrenceFrequency.Daily:
                parts.Add(Interval == 1 ? "Daily" : $"Every {Interval} days");
                break;

            case RecurrenceFrequency.Weekly:
                if (Interval == 1)
                {
                    if (DaysOfWeek.Count == 1)
                        parts.Add($"Weekly on {DaysOfWeek.First()}");
                    else if (DaysOfWeek.Count > 1)
                        parts.Add($"Weekly on {string.Join(", ", DaysOfWeek)}");
                    else
                        parts.Add("Weekly");
                }
                else
                {
                    parts.Add($"Every {Interval} weeks");
                    if (DaysOfWeek.Any())
                        parts.Add($"on {string.Join(", ", DaysOfWeek)}");
                }
                break;

            case RecurrenceFrequency.Monthly:
                if (DayOfMonth.HasValue)
                {
                    parts.Add(Interval == 1 
                        ? $"Monthly on day {DayOfMonth}" 
                        : $"Every {Interval} months on day {DayOfMonth}");
                }
                else if (WeekOfMonth.HasValue && DaysOfWeek.Any())
                {
                    var weekDesc = WeekOfMonth == -1 ? "last" : GetOrdinal(WeekOfMonth.Value);
                    parts.Add(Interval == 1 
                        ? $"Monthly on the {weekDesc} {DaysOfWeek.First()}" 
                        : $"Every {Interval} months on the {weekDesc} {DaysOfWeek.First()}");
                }
                break;

            case RecurrenceFrequency.Yearly:
                if (DayOfMonth.HasValue)
                {
                    var monthName = GetMonthName(MonthOfYear ?? 1);
                    parts.Add(Interval == 1 
                        ? $"Yearly on {monthName} {DayOfMonth}" 
                        : $"Every {Interval} years on {monthName} {DayOfMonth}");
                }
                else if (WeekOfMonth.HasValue && DaysOfWeek.Any())
                {
                    var weekDesc = WeekOfMonth == -1 ? "last" : GetOrdinal(WeekOfMonth.Value);
                    var monthName = GetMonthName(MonthOfYear ?? 1);
                    parts.Add(Interval == 1 
                        ? $"Yearly on the {weekDesc} {DaysOfWeek.First()} of {monthName}" 
                        : $"Every {Interval} years on the {weekDesc} {DaysOfWeek.First()} of {monthName}");
                }
                break;
        }

        // Add end condition
        switch (EndType)
        {
            case RecurrenceEndType.Date when EndDate.HasValue:
                parts.Add($"until {EndDate.Value:MMM dd, yyyy}");
                break;
            case RecurrenceEndType.Count when OccurrenceCount.HasValue:
                parts.Add($"for {OccurrenceCount} occurrences");
                break;
        }

        return string.Join(", ", parts);
    }

    private static string GetOrdinal(int number)
    {
        return number switch
        {
            1 => "first",
            2 => "second", 
            3 => "third",
            4 => "fourth",
            5 => "fifth",
            _ => $"{number}th"
        };
    }

    private static string GetMonthName(int month)
    {
        return new DateTime(2000, month, 1).ToString("MMMM");
    }

    private static bool TryParseDayAbbreviation(string dayAbbrev, out DayOfWeek dayOfWeek)
    {
        dayOfWeek = dayAbbrev.ToUpper() switch
        {
            "SU" => DayOfWeek.Sunday,
            "MO" => DayOfWeek.Monday,
            "TU" => DayOfWeek.Tuesday,
            "WE" => DayOfWeek.Wednesday,
            "TH" => DayOfWeek.Thursday,
            "FR" => DayOfWeek.Friday,
            "SA" => DayOfWeek.Saturday,
            _ => default
        };
        
        return dayAbbrev.Length == 2;
    }
}

/// <summary>
/// Frequency of event recurrence
/// </summary>
public enum RecurrenceFrequency
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
    Yearly = 3
}

/// <summary>
/// How the event recurrence ends
/// </summary>
public enum RecurrenceEndType
{
    Never = 0,     // No end date, infinite recurrence
    Date = 1,      // Ends on a specific date
    Count = 2      // Ends after a specific number of occurrences
}