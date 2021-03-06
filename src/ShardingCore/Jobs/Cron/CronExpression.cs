/* 
* Copyright 2004-2009 James House 
* 
* Licensed under the Apache License, Version 2.0 (the "License"); you may not 
* use this file except in compliance with the License. You may obtain a copy 
* of the License at 
* 
*   http://www.apache.org/licenses/LICENSE-2.0 
*   
* Unless required by applicable law or agreed to in writing, software 
* distributed under the License is distributed on an "AS IS" BASIS, WITHOUT 
* WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
* License for the specific language governing permissions and limitations 
* under the License.
* 
*/


using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text;


//using TimeZone = System.TimeZoneInfo;


namespace ShardingCore.Jobs.Cron
{
    /// <summary>
    /// Provides a parser and evaluator for unix-like cron expressions. Cron 
    /// expressions provide the ability to specify complex time combinations such as
    /// &quot;At 8:00am every Monday through Friday&quot; or &quot;At 1:30am every 
    /// last Friday of the month&quot;. 
    /// </summary>
    /// <remarks>
    /// <p>
    /// Cron expressions are comprised of 6 required fields and one optional field
    /// separated by white space. The fields respectively are described as follows:
    /// </p>
    /// <table cellspacing="8">
    /// <tr>
    /// <th align="left">Field Name</th>
    /// <th align="left"> </th>
    /// <th align="left">Allowed Values</th>
    /// <th align="left"> </th>
    /// <th align="left">Allowed Special Characters</th>
    /// </tr>
    /// <tr>
    /// <td align="left">Seconds</td>
    /// <td align="left"> </td>
    /// <td align="left">0-59</td>
    /// <td align="left"> </td>
    /// <td align="left">, - /// /</td>
    /// </tr>
    /// <tr>
    /// <td align="left">Minutes</td>
    /// <td align="left"> </td>
    /// <td align="left">0-59</td>
    /// <td align="left"> </td>
    /// <td align="left">, - /// /</td>
    /// </tr>
    /// <tr>
    /// <td align="left">Hours</td>
    /// <td align="left"> </td>
    /// <td align="left">0-23</td>
    /// <td align="left"> </td>
    /// <td align="left">, - /// /</td>
    /// </tr>
    /// <tr>
    /// <td align="left">Day-of-month</td>
    /// <td align="left"> </td>
    /// <td align="left">1-31</td>
    /// <td align="left"> </td>
    /// <td align="left">, - /// ? / L W C</td>
    /// </tr>
    /// <tr>
    /// <td align="left">Month</td>
    /// <td align="left"> </td>
    /// <td align="left">1-12 or JAN-DEC</td>
    /// <td align="left"> </td>
    /// <td align="left">, - /// /</td>
    /// </tr>
    /// <tr>
    /// <td align="left">Day-of-Week</td>
    /// <td align="left"> </td>
    /// <td align="left">1-7 or SUN-SAT</td>
    /// <td align="left"> </td>
    /// <td align="left">, - /// ? / L #</td>
    /// </tr>
    /// <tr>
    /// <td align="left">Year (Optional)</td>
    /// <td align="left"> </td>
    /// <td align="left">empty, 1970-2099</td>
    /// <td align="left"> </td>
    /// <td align="left">, - /// /</td>
    /// </tr>
    /// </table>
    /// <p>
    /// The '*' character is used to specify all values. For example, &quot;*&quot; 
    /// in the minute field means &quot;every minute&quot;.
    /// </p>
    /// <p>
    /// The '?' character is allowed for the day-of-month and day-of-week fields. It
    /// is used to specify 'no specific value'. This is useful when you need to
    /// specify something in one of the two fields, but not the other.
    /// </p>
    /// <p>
    /// The '-' character is used to specify ranges For example &quot;10-12&quot; in
    /// the hour field means &quot;the hours 10, 11 and 12&quot;.
    /// </p>
    /// <p>
    /// The ',' character is used to specify additional values. For example
    /// &quot;MON,WED,FRI&quot; in the day-of-week field means &quot;the days Monday,
    /// Wednesday, and Friday&quot;.
    /// </p>
    /// <p>
    /// The '/' character is used to specify increments. For example &quot;0/15&quot;
    /// in the seconds field means &quot;the seconds 0, 15, 30, and 45&quot;. And 
    /// &quot;5/15&quot; in the seconds field means &quot;the seconds 5, 20, 35, and
    /// 50&quot;.  Specifying '*' before the  '/' is equivalent to specifying 0 is
    /// the value to start with. Essentially, for each field in the expression, there
    /// is a set of numbers that can be turned on or off. For seconds and minutes, 
    /// the numbers range from 0 to 59. For hours 0 to 23, for days of the month 0 to
    /// 31, and for months 1 to 12. The &quot;/&quot; character simply helps you turn
    /// on every &quot;nth&quot; value in the given set. Thus &quot;7/6&quot; in the
    /// month field only turns on month &quot;7&quot;, it does NOT mean every 6th 
    /// month, please note that subtlety.  
    /// </p>
    /// <p>
    /// The 'L' character is allowed for the day-of-month and day-of-week fields.
    /// This character is short-hand for &quot;last&quot;, but it has different 
    /// meaning in each of the two fields. For example, the value &quot;L&quot; in 
    /// the day-of-month field means &quot;the last day of the month&quot; - day 31 
    /// for January, day 28 for February on non-leap years. If used in the 
    /// day-of-week field by itself, it simply means &quot;7&quot; or 
    /// &quot;SAT&quot;. But if used in the day-of-week field after another value, it
    /// means &quot;the last xxx day of the month&quot; - for example &quot;6L&quot;
    /// means &quot;the last friday of the month&quot;. When using the 'L' option, it
    /// is important not to specify lists, or ranges of values, as you'll get 
    /// confusing results.
    /// </p>
    /// <p>
    /// The 'W' character is allowed for the day-of-month field.  This character 
    /// is used to specify the weekday (Monday-Friday) nearest the given day.  As an 
    /// example, if you were to specify &quot;15W&quot; as the value for the 
    /// day-of-month field, the meaning is: &quot;the nearest weekday to the 15th of
    /// the month&quot;. So if the 15th is a Saturday, the trigger will fire on 
    /// Friday the 14th. If the 15th is a Sunday, the trigger will fire on Monday the
    /// 16th. If the 15th is a Tuesday, then it will fire on Tuesday the 15th. 
    /// However if you specify &quot;1W&quot; as the value for day-of-month, and the
    /// 1st is a Saturday, the trigger will fire on Monday the 3rd, as it will not 
    /// 'jump' over the boundary of a month's days.  The 'W' character can only be 
    /// specified when the day-of-month is a single day, not a range or list of days.
    /// </p>
    /// <p>
    /// The 'L' and 'W' characters can also be combined for the day-of-month 
    /// expression to yield 'LW', which translates to &quot;last weekday of the 
    /// month&quot;.
    /// </p>
    /// <p>
    /// The '#' character is allowed for the day-of-week field. This character is
    /// used to specify &quot;the nth&quot; XXX day of the month. For example, the 
    /// value of &quot;6#3&quot; in the day-of-week field means the third Friday of 
    /// the month (day 6 = Friday and &quot;#3&quot; = the 3rd one in the month). 
    /// Other examples: &quot;2#1&quot; = the first Monday of the month and 
    /// &quot;4#5&quot; = the fifth Wednesday of the month. Note that if you specify
    /// &quot;#5&quot; and there is not 5 of the given day-of-week in the month, then
    /// no firing will occur that month. If the '#' character is used, there can
    /// only be one expression in the day-of-week field (&quot;3#1,6#3&quot; is 
    /// not valid, since there are two expressions).
    /// </p>
    /// <p>
    /// <!--The 'C' character is allowed for the day-of-month and day-of-week fields.
    /// This character is short-hand for "calendar". This means values are
    /// calculated against the associated calendar, if any. If no calendar is
    /// associated, then it is equivalent to having an all-inclusive calendar. A
    /// value of "5C" in the day-of-month field means "the first day included by the
    /// calendar on or after the 5th". A value of "1C" in the day-of-week field
    /// means "the first day included by the calendar on or after sunday". -->
    /// </p>
    /// <p>
    /// The legal characters and the names of months and days of the week are not
    /// case sensitive.
    /// </p>
    /// <p>
    /// <b>NOTES:</b>
    /// <ul>
    /// <li>Support for specifying both a day-of-week and a day-of-month value is
    /// not complete (you'll need to use the '?' character in one of these fields).
    /// </li>
    /// <li>Overflowing ranges is supported - that is, having a larger number on 
    /// the left hand side than the right. You might do 22-2 to catch 10 o'clock 
    /// at night until 2 o'clock in the morning, or you might have NOV-FEB. It is 
    /// very important to note that overuse of overflowing ranges creates ranges 
    /// that don't make sense and no effort has been made to determine which 
    /// interpretation CronExpression chooses. An example would be 
    /// "0 0 14-6 ? * FRI-MON". </li>
    /// </ul>
    /// </p>
    /// </remarks>
    /// <author>Sharada Jambula</author>
    /// <author>James House</author>
    /// <author>Contributions from Mads Henderson</author>
    /// <author>Refactoring from CronTrigger to CronExpression by Aaron Craven</author>
    [Serializable]
    [ExcludeFromCodeCoverage]
    internal class CronExpression : ICloneable, IDeserializationCallback
    {
		/// <summary>
		/// Field specification for second.
		/// </summary>
        protected const int Second = 0;

		/// <summary>
		/// Field specification for minute.
		/// </summary>
		protected const int Minute = 1;
        
		/// <summary>
		/// Field specification for hour.
		/// </summary>
		protected const int Hour = 2;
        
		/// <summary>
		/// Field specification for day of month.
		/// </summary>
		protected const int DayOfMonth = 3;
        
		/// <summary>
		/// Field specification for month.
		/// </summary>
		protected const int Month = 4;
        
		/// <summary>
		/// Field specification for day of week.
		/// </summary>
		protected const int DayOfWeek = 5;
        
		/// <summary>
		/// Field specification for year.
		/// </summary>
		protected const int Year = 6;
        
		/// <summary>
		/// Field specification for all wildcard value '*'.
		/// </summary>
		protected const int AllSpecInt = 99; // '*'
        
		/// <summary>
		/// Field specification for not specified value '?'.
		/// </summary>
		protected const int NoSpecInt = 98; // '?'
        
		/// <summary>
		/// Field specification for wildcard '*'.
		/// </summary>
		protected const int AllSpec = AllSpecInt;
		
		/// <summary>
		/// Field specification for no specification at all '?'.
		/// </summary>
		protected const int NoSpec = NoSpecInt;

        private static readonly Hashtable monthMap = new Hashtable(20);
        private static readonly Hashtable dayMap = new Hashtable(60);

        private readonly string cronExpressionString = null;
        
        private TimeZone timeZone = null;

        /// <summary>
        /// Seconds.
        /// </summary>
        [NonSerialized]
        protected TreeSet seconds;
        /// <summary>
        /// minutes.
        /// </summary>
        [NonSerialized]
        protected TreeSet minutes;
        /// <summary>
        /// Hours.
        /// </summary>
        [NonSerialized]
        protected TreeSet hours;
        /// <summary>
        /// Days of month.
        /// </summary>
        [NonSerialized]
        protected TreeSet daysOfMonth;
        /// <summary>
        /// Months.
        /// </summary>
        [NonSerialized]
        protected TreeSet months;
        /// <summary>
        /// Days of week.
        /// </summary>
        [NonSerialized]
        protected TreeSet daysOfWeek;
        /// <summary>
        /// Years.
        /// </summary>
        [NonSerialized]
        protected TreeSet years;

        /// <summary>
        /// Last day of week.
        /// </summary>
        [NonSerialized]
        protected bool lastdayOfWeek = false;
        /// <summary>
        /// Nth day of week.
        /// </summary>
        [NonSerialized]
        protected int nthdayOfWeek = 0;
        /// <summary>
        /// Last day of month.
        /// </summary>
        [NonSerialized]
        protected bool lastdayOfMonth = false;
        /// <summary>
        /// Nearest weekday.
        /// </summary>
        [NonSerialized]
        protected bool nearestWeekday = false;
        /// <summary>
        /// Calendar day of week.
        /// </summary>
        [NonSerialized]
        protected bool calendardayOfWeek = false;
        /// <summary>
        /// Calendar day of month.
        /// </summary>
        [NonSerialized]
        protected bool calendardayOfMonth = false;
        /// <summary>
        /// Expression parsed.
        /// </summary>
        [NonSerialized]
        protected bool expressionParsed = false;

        static CronExpression()
        {
            monthMap.Add("JAN", 0);
            monthMap.Add("FEB", 1);
            monthMap.Add("MAR", 2);
            monthMap.Add("APR", 3);
            monthMap.Add("MAY", 4);
            monthMap.Add("JUN", 5);
            monthMap.Add("JUL", 6);
            monthMap.Add("AUG", 7);
            monthMap.Add("SEP", 8);
            monthMap.Add("OCT", 9);
            monthMap.Add("NOV", 10);
            monthMap.Add("DEC", 11);

            dayMap.Add("SUN", 1);
            dayMap.Add("MON", 2);
            dayMap.Add("TUE", 3);
            dayMap.Add("WED", 4);
            dayMap.Add("THU", 5);
            dayMap.Add("FRI", 6);
            dayMap.Add("SAT", 7);
        }

        ///<summary>
        /// Constructs a new <see cref="CronExpressionString" /> based on the specified 
        /// parameter.
        /// </summary>
        /// <param name="cronExpression">
        /// String representation of the cron expression the new object should represent
        /// </param>
        /// <see cref="CronExpressionString" />
        public CronExpression(string cronExpression)
        {
            if (cronExpression == null)
            {
                throw new ArgumentException("cronExpression cannot be null");
            }

            cronExpressionString = cronExpression.ToUpper(CultureInfo.InvariantCulture);
            BuildExpression(cronExpression);
        }

        /// <summary>
        /// Indicates whether the given date satisfies the cron expression. 
        /// </summary>
        /// <remarks>
        /// Note that  milliseconds are ignored, so two Dates falling on different milliseconds
        /// of the same second will always have the same result here.
        /// </remarks>
        /// <param name="dateUtc">The date to evaluate.</param>
        /// <returns>a boolean indicating whether the given date satisfies the cron expression</returns>
        public virtual bool IsSatisfiedBy(DateTime dateUtc)
        {
            DateTime test =
                new DateTime(dateUtc.Year, dateUtc.Month, dateUtc.Day, dateUtc.Hour, dateUtc.Minute, dateUtc.Second).AddSeconds(-1);

            DateTime? timeAfter = GetTimeAfter(test);

            if (timeAfter.HasValue && timeAfter.Value.Equals(dateUtc))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the next date/time <i>after</i> the given date/time which
        /// satisfies the cron expression.
        /// </summary>
        /// <param name="date">the date/time at which to begin the search for the next valid date/time</param>
        /// <returns>the next valid date/time</returns>
        public virtual DateTime? GetNextValidTimeAfter(DateTime date)
        {
            return GetTimeAfter(date);
        }

        /// <summary>
        /// Returns the next date/time <i>after</i> the given date/time which does
        /// <i>not</i> satisfy the expression.
        /// </summary>
        /// <param name="date">the date/time at which to begin the search for the next invalid date/time</param>
        /// <returns>the next valid date/time</returns>
        public virtual DateTime? GetNextInvalidTimeAfter(DateTime date)
        {
            long difference = 1000;

            //move back to the nearest second so differences will be accurate
            DateTime lastDate =
                new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second).AddSeconds(-1);

            //TODO: IMPROVE THIS! The following is a BAD solution to this problem. Performance will be very bad here, depending on the cron expression. It is, however A solution.

            //keep getting the next included time until it's farther than one second
            // apart. At that point, lastDate is the last valid fire time. We return
            // the second immediately following it.
            while (difference == 1000)
            {
                DateTime newDate = GetTimeAfter(lastDate).Value;

                difference = (long) (newDate - lastDate).TotalMilliseconds;

                if (difference == 1000)
                {
                    lastDate = newDate;
                }
            }

            return lastDate.AddSeconds(1);
        }

        /// <summary>
        /// Sets or gets the time zone for which the <see cref="CronExpression" /> of this
        /// <see cref="CronTrigger" /> will be resolved.
        /// </summary>
        public virtual TimeZone TimeZone
        {
            set { timeZone = value; }
            get
            {
                if (timeZone == null)
                {
                    timeZone = TimeZone.CurrentTimeZone;
                }

                return timeZone;
            }
        }

        /// <summary>
        /// Returns the string representation of the <see cref="CronExpression" />
        /// </summary>
        /// <returns>The string representation of the <see cref="CronExpression" /></returns>
        public override string ToString()
        {
            return cronExpressionString;
        }

        /// <summary>
        /// Indicates whether the specified cron expression can be parsed into a 
        /// valid cron expression
        /// </summary>
        /// <param name="cronExpression">the expression to evaluate</param>
        /// <returns>a boolean indicating whether the given expression is a valid cron
        ///         expression</returns>
        public static bool IsValidExpression(string cronExpression)
        {
            try
            {
                new CronExpression(cronExpression);
            }
            catch (FormatException)
            {
                return false;
            }

            return true;
        }

        ////////////////////////////////////////////////////////////////////////////
        //
        // Expression Parsing Functions
        //
        ////////////////////////////////////////////////////////////////////////////

		/// <summary>
		/// Builds the expression.
		/// </summary>
		/// <param name="expression">The expression.</param>
        protected void BuildExpression(string expression)
        {
            expressionParsed = true;

            try
            {
                if (seconds == null)
                {
                    seconds = new TreeSet();
                }
                if (minutes == null)
                {
                    minutes = new TreeSet();
                }
                if (hours == null)
                {
                    hours = new TreeSet();
                }
                if (daysOfMonth == null)
                {
                    daysOfMonth = new TreeSet();
                }
                if (months == null)
                {
                    months = new TreeSet();
                }
                if (daysOfWeek == null)
                {
                    daysOfWeek = new TreeSet();
                }
                if (years == null)
                {
                    years = new TreeSet();
                }

                int exprOn = Second;

#if NET_20
                string[] exprsTok = expression.Trim().Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
#else
				string[] exprsTok = expression.Trim().Split(new char[] { ' ', '\t', '\r', '\n' });
#endif
                foreach (string exprTok in exprsTok)
                {
                    string expr = exprTok.Trim();

					if (expr.Length == 0)
					{
						continue;
					}
                    if (exprOn > Year)
                    {
                        break;
                    }

                    // throw an exception if L is used with other days of the month
                    if (exprOn == DayOfMonth && expr.IndexOf('L') != -1 && expr.Length > 1 && expr.IndexOf(",") >= 0)
                    {
                        throw new FormatException("Support for specifying 'L' and 'LW' with other days of the month is not implemented");
                    }
                    // throw an exception if L is used with other days of the week
                    if (exprOn == DayOfWeek && expr.IndexOf('L') != -1 && expr.Length > 1 && expr.IndexOf(",") >= 0)
                    {
                        throw new FormatException("Support for specifying 'L' with other days of the week is not implemented");
                    }

                    string[] vTok = expr.Split(',');
                    foreach (string v in vTok)
                    {
                        StoreExpressionVals(0, v, exprOn);
                    }

                    exprOn++;
                }

                if (exprOn <= DayOfWeek)
                {
                    throw new FormatException("Unexpected end of expression.");
                }

                if (exprOn <= Year)
                {
                    StoreExpressionVals(0, "*", Year);
                }

                TreeSet dow = GetSet(DayOfWeek);
                TreeSet dom = GetSet(DayOfMonth);

                // Copying the logic from the UnsupportedOperationException below
                bool dayOfMSpec = !dom.Contains(NoSpec);
                bool dayOfWSpec = !dow.Contains(NoSpec);

                if (dayOfMSpec && !dayOfWSpec)
                {
                    // skip
                }
                else if (dayOfWSpec && !dayOfMSpec)
                {
                    // skip
                }
                else
                {
                    throw new FormatException("Support for specifying both a day-of-week AND a day-of-month parameter is not implemented.");
                }
            }
            catch (FormatException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new FormatException(string.Format(CultureInfo.InvariantCulture, "Illegal cron expression format ({0})", e));
            }
        }

		/// <summary>
		/// Stores the expression values.
		/// </summary>
		/// <param name="pos">The position.</param>
		/// <param name="s">The string to traverse.</param>
		/// <param name="type">The type of value.</param>
		/// <returns></returns>
        protected virtual int StoreExpressionVals(int pos, string s, int type)
        {
            int incr = 0;
            int i = SkipWhiteSpace(pos, s);
            if (i >= s.Length)
            {
                return i;
            }
            char c = s[i];
            if ((c >= 'A') && (c <= 'Z') && (!s.Equals("L")) && (!s.Equals("LW")))
            {
                String sub = s.Substring(i, 3);
                int sval;
                int eval = -1;
                if (type == Month)
                {
                    sval = GetMonthNumber(sub) + 1;
                    if (sval <= 0)
                    {
                        throw new FormatException(string.Format(CultureInfo.InvariantCulture, "Invalid Month value: '{0}'", sub));
                    }
                    if (s.Length > i + 3)
                    {
                        c = s[i + 3];
                        if (c == '-')
                        {
                            i += 4;
                            sub = s.Substring(i, 3);
                            eval = GetMonthNumber(sub) + 1;
                            if (eval <= 0)
                            {
                                throw new FormatException(
                                    string.Format(CultureInfo.InvariantCulture, "Invalid Month value: '{0}'", sub));
                            }
                        }
                    }
                }
                else if (type == DayOfWeek)
                {
                    sval = GetDayOfWeekNumber(sub);
                    if (sval < 0)
                    {
                        throw new FormatException(string.Format(CultureInfo.InvariantCulture, "Invalid Day-of-Week value: '{0}'", sub));
                    }
                    if (s.Length > i + 3)
                    {
                        c = s[i + 3];
                        if (c == '-')
                        {
                            i += 4;
                            sub = s.Substring(i, 3);
                            eval = GetDayOfWeekNumber(sub);
                            if (eval < 0)
                            {
                                throw new FormatException(
                                    string.Format(CultureInfo.InvariantCulture, "Invalid Day-of-Week value: '{0}'", sub));
                            }
                        }
                        else if (c == '#')
                        {
                            try
                            {
                                i += 4;
                                nthdayOfWeek = Convert.ToInt32(s.Substring(i), CultureInfo.InvariantCulture);
                                if (nthdayOfWeek < 1 || nthdayOfWeek > 5)
                                {
                                    throw new Exception();
                                }
                            }
                            catch (Exception)
                            {
                                throw new FormatException(
                                    "A numeric value between 1 and 5 must follow the '#' option");
                            }
                        }
                        else if (c == 'L')
                        {
                            lastdayOfWeek = true;
                            i++;
                        }
                    }
                }
                else
                {
                    throw new FormatException(
                        string.Format(CultureInfo.InvariantCulture, "Illegal characters for this position: '{0}'", sub));
                }
                if (eval != -1)
                {
                    incr = 1;
                }
                AddToSet(sval, eval, incr, type);
                return (i + 3);
            }

            if (c == '?')
            {
                i++;
                if ((i + 1) < s.Length
                    && (s[i] != ' ' && s[i + 1] != '\t'))
                {
                    throw new FormatException("Illegal character after '?': "
                                              + s[i]);
                }
                if (type != DayOfWeek && type != DayOfMonth)
                {
                    throw new FormatException(
                        "'?' can only be specified for Day-of-Month or Day-of-Week.");
                }
                if (type == DayOfWeek && !lastdayOfMonth)
                {
                    int val = (int) daysOfMonth[daysOfMonth.Count - 1];
                    if (val == NoSpecInt)
                    {
                        throw new FormatException(
                            "'?' can only be specified for Day-of-Month -OR- Day-of-Week.");
                    }
                }

                AddToSet(NoSpecInt, -1, 0, type);
                return i;
            }

            if (c == '*' || c == '/')
            {
                if (c == '*' && (i + 1) >= s.Length)
                {
                    AddToSet(AllSpecInt, -1, incr, type);
                    return i + 1;
                }
                else if (c == '/'
                         && ((i + 1) >= s.Length || s[i + 1] == ' ' || s[i + 1] == '\t'))
                {
                    throw new FormatException("'/' must be followed by an integer.");
                }
                else if (c == '*')
                {
                    i++;
                }
                c = s[i];
                if (c == '/')
                {
                    // is an increment specified?
                    i++;
                    if (i >= s.Length)
                    {
                        throw new FormatException("Unexpected end of string.");
                    }

                    incr = GetNumericValue(s, i);

                    i++;
                    if (incr > 10)
                    {
                        i++;
                    }
                    if (incr > 59 && (type == Second || type == Minute))
                    {
                        throw new FormatException(
                            string.Format(CultureInfo.InvariantCulture, "Increment > 60 : {0}", incr));
                    }
                    else if (incr > 23 && (type == Hour))
                    {
                        throw new FormatException(
                            string.Format(CultureInfo.InvariantCulture, "Increment > 24 : {0}", incr));
                    }
                    else if (incr > 31 && (type == DayOfMonth))
                    {
                        throw new FormatException(
                            string.Format(CultureInfo.InvariantCulture, "Increment > 31 : {0}", incr));
                    }
                    else if (incr > 7 && (type == DayOfWeek))
                    {
                        throw new FormatException(
                            string.Format(CultureInfo.InvariantCulture, "Increment > 7 : {0}", incr));
                    }
                    else if (incr > 12 && (type == Month))
                    {
                        throw new FormatException(string.Format(CultureInfo.InvariantCulture, "Increment > 12 : {0}", incr));
                    }
                }
                else
                {
                    incr = 1;
                }

                AddToSet(AllSpecInt, -1, incr, type);
                return i;
            }
            else if (c == 'L')
            {
                i++;
                if (type == DayOfMonth)
                {
                    lastdayOfMonth = true;
                }
                if (type == DayOfWeek)
                {
                    AddToSet(7, 7, 0, type);
                }
                if (type == DayOfMonth && s.Length > i)
                {
                    c = s[i];
                    if (c == 'W')
                    {
                        nearestWeekday = true;
                        i++;
                    }
                }
                return i;
            }
            else if (c >= '0' && c <= '9')
            {
                int val = Convert.ToInt32(c.ToString(), CultureInfo.InvariantCulture);
                i++;
                if (i >= s.Length)
                {
                    AddToSet(val, -1, -1, type);
                }
                else
                {
                    c = s[i];
                    if (c >= '0' && c <= '9')
                    {
                        ValueSet vs = GetValue(val, s, i);
                        val = vs.theValue;
                        i = vs.pos;
                    }
                    i = CheckNext(i, s, val, type);
                    return i;
                }
            }
            else
            {
                throw new FormatException(string.Format(CultureInfo.InvariantCulture, "Unexpected character: {0}", c));
            }

            return i;
        }

		/// <summary>
		/// Checks the next value.
		/// </summary>
		/// <param name="pos">The position.</param>
		/// <param name="s">The string to check.</param>
		/// <param name="val">The value.</param>
		/// <param name="type">The type to search.</param>
		/// <returns></returns>
        protected virtual int CheckNext(int pos, string s, int val, int type)
        {
            int end = -1;
            int i = pos;

            if (i >= s.Length)
            {
                AddToSet(val, end, -1, type);
                return i;
            }

            char c = s[pos];

            if (c == 'L')
            {
                if (type == DayOfWeek)
                {
                    lastdayOfWeek = true;
                }
                else
                {
                    throw new FormatException(string.Format(CultureInfo.InvariantCulture, "'L' option is not valid here. (pos={0})", i));
                }
                TreeSet data = GetSet(type);
                data.Add(val);
                i++;
                return i;
            }

            if (c == 'W')
            {
                if (type == DayOfMonth)
                {
                    nearestWeekday = true;
                }
                else
                {
                    throw new FormatException(string.Format(CultureInfo.InvariantCulture, "'W' option is not valid here. (pos={0})", i));
                }
                TreeSet data = GetSet(type);
                data.Add(val);
                i++;
                return i;
            }

            if (c == '#')
            {
                if (type != DayOfWeek)
                {
                    throw new FormatException(
                        string.Format(CultureInfo.InvariantCulture, "'#' option is not valid here. (pos={0})", i));
                }
                i++;
                try
                {
                    nthdayOfWeek = Convert.ToInt32(s.Substring(i), CultureInfo.InvariantCulture);
                    if (nthdayOfWeek < 1 || nthdayOfWeek > 5)
                    {
                        throw new Exception();
                    }
                }
                catch (Exception)
                {
                    throw new FormatException(
                        "A numeric value between 1 and 5 must follow the '#' option");
                }

                TreeSet data = GetSet(type);
                data.Add(val);
                i++;
                return i;
            }

            if (c == 'C')
            {
                if (type == DayOfWeek)
                {
                    calendardayOfWeek = true;
                }
                else if (type == DayOfMonth)
                {
                    calendardayOfMonth = true;
                }
                else
                {
                    throw new FormatException(string.Format(CultureInfo.InvariantCulture, "'C' option is not valid here. (pos={0})", i));
                }
                TreeSet data = GetSet(type);
                data.Add(val);
                i++;
                return i;
            }

            if (c == '-')
            {
                i++;
                c = s[i];
                int v = Convert.ToInt32(c.ToString(), CultureInfo.InvariantCulture);
                end = v;
                i++;
                if (i >= s.Length)
                {
                    AddToSet(val, end, 1, type);
                    return i;
                }
                c = s[i];
                if (c >= '0' && c <= '9')
                {
                    ValueSet vs = GetValue(v, s, i);
                    int v1 = vs.theValue;
                    end = v1;
                    i = vs.pos;
                }
                if (i < s.Length && ((c = s[i]) == '/'))
                {
                    i++;
                    c = s[i];
                    int v2 = Convert.ToInt32(c.ToString(), CultureInfo.InvariantCulture);
                    i++;
                    if (i >= s.Length)
                    {
                        AddToSet(val, end, v2, type);
                        return i;
                    }
                    c = s[i];
                    if (c >= '0' && c <= '9')
                    {
                        ValueSet vs = GetValue(v2, s, i);
                        int v3 = vs.theValue;
                        AddToSet(val, end, v3, type);
                        i = vs.pos;
                        return i;
                    }
                    else
                    {
                        AddToSet(val, end, v2, type);
                        return i;
                    }
                }
                else
                {
                    AddToSet(val, end, 1, type);
                    return i;
                }
            }

            if (c == '/')
            {
                i++;
                c = s[i];
                int v2 = Convert.ToInt32(c.ToString(), CultureInfo.InvariantCulture);
                i++;
                if (i >= s.Length)
                {
                    AddToSet(val, end, v2, type);
                    return i;
                }
                c = s[i];
                if (c >= '0' && c <= '9')
                {
                    ValueSet vs = GetValue(v2, s, i);
                    int v3 = vs.theValue;
                    AddToSet(val, end, v3, type);
                    i = vs.pos;
                    return i;
                }
                else
                {
                    throw new FormatException(string.Format(CultureInfo.InvariantCulture, "Unexpected character '{0}' after '/'", c));
                }
            }

            AddToSet(val, end, 0, type);
            i++;
            return i;
        }
		/// <summary>
		/// Gets the cron expression string.
		/// </summary>
		/// <value>The cron expression string.</value>
        public string CronExpressionString
        {
            get { return cronExpressionString; }
        }

		/// <summary>
		/// Gets the expression summary.
		/// </summary>
		/// <returns></returns>
        public virtual string GetExpressionSummary()
        {
            StringBuilder buf = new StringBuilder();

            buf.Append("seconds: ");
            buf.Append(GetExpressionSetSummary(seconds));
            buf.Append("\n");
            buf.Append("minutes: ");
            buf.Append(GetExpressionSetSummary(minutes));
            buf.Append("\n");
            buf.Append("hours: ");
            buf.Append(GetExpressionSetSummary(hours));
            buf.Append("\n");
            buf.Append("daysOfMonth: ");
            buf.Append(GetExpressionSetSummary(daysOfMonth));
            buf.Append("\n");
            buf.Append("months: ");
            buf.Append(GetExpressionSetSummary(months));
            buf.Append("\n");
            buf.Append("daysOfWeek: ");
            buf.Append(GetExpressionSetSummary(daysOfWeek));
            buf.Append("\n");
            buf.Append("lastdayOfWeek: ");
            buf.Append(lastdayOfWeek);
            buf.Append("\n");
            buf.Append("nearestWeekday: ");
            buf.Append(nearestWeekday);
            buf.Append("\n");
            buf.Append("NthDayOfWeek: ");
            buf.Append(nthdayOfWeek);
            buf.Append("\n");
            buf.Append("lastdayOfMonth: ");
            buf.Append(lastdayOfMonth);
            buf.Append("\n");
            buf.Append("calendardayOfWeek: ");
            buf.Append(calendardayOfWeek);
            buf.Append("\n");
            buf.Append("calendardayOfMonth: ");
            buf.Append(calendardayOfMonth);
            buf.Append("\n");
            buf.Append("years: ");
            buf.Append(GetExpressionSetSummary(years));
            buf.Append("\n");

            return buf.ToString();
        }

		/// <summary>
		/// Gets the expression set summary.
		/// </summary>
		/// <param name="data">The data.</param>
		/// <returns></returns>
        protected virtual string GetExpressionSetSummary(ISet data)
        {
            if (data.Contains(NoSpec))
            {
                return "?";
            }
            if (data.Contains(AllSpec))
            {
                return "*";
            }

            StringBuilder buf = new StringBuilder();

            bool first = true;
            foreach (int iVal in data)
            {
                string val = iVal.ToString(CultureInfo.InvariantCulture);
                if (!first)
                {
                    buf.Append(",");
                }
                buf.Append(val);
                first = false;
            }

            return buf.ToString();
        }

		/// <summary>
		/// Skips the white space.
		/// </summary>
		/// <param name="i">The i.</param>
		/// <param name="s">The s.</param>
		/// <returns></returns>
        protected virtual int SkipWhiteSpace(int i, string s)
        {
            for (; i < s.Length && (s[i] == ' ' || s[i] == '\t'); i++)
            {
                ;
            }

            return i;
        }

        /// <summary>
        /// Finds the next white space.
        /// </summary>
        /// <param name="i">The i.</param>
        /// <param name="s">The s.</param>
        /// <returns></returns>
        protected virtual int FindNextWhiteSpace(int i, string s)
        {
            for (; i < s.Length && (s[i] != ' ' || s[i] != '\t'); i++)
            {
                ;
            }

            return i;
        }

        /// <summary>
        /// Adds to set.
        /// </summary>
        /// <param name="val">The val.</param>
        /// <param name="end">The end.</param>
        /// <param name="incr">The incr.</param>
        /// <param name="type">The type.</param>
        protected virtual void AddToSet(int val, int end, int incr, int type)
        {
            TreeSet data = GetSet(type);

            if (type == Second || type == Minute)
            {
                if ((val < 0 || val > 59 || end > 59) && (val != AllSpecInt))
                {
                    throw new FormatException(
                        "Minute and Second values must be between 0 and 59");
                }
            }
            else if (type == Hour)
            {
                if ((val < 0 || val > 23 || end > 23) && (val != AllSpecInt))
                {
                    throw new FormatException(
                        "Hour values must be between 0 and 23");
                }
            }
            else if (type == DayOfMonth)
            {
                if ((val < 1 || val > 31 || end > 31) && (val != AllSpecInt)
                    && (val != NoSpecInt))
                {
                    throw new FormatException(
                        "Day of month values must be between 1 and 31");
                }
            }
            else if (type == Month)
            {
                if ((val < 1 || val > 12 || end > 12) && (val != AllSpecInt))
                {
                    throw new FormatException(
                        "Month values must be between 1 and 12");
                }
            }
            else if (type == DayOfWeek)
            {
                if ((val == 0 || val > 7 || end > 7) && (val != AllSpecInt)
                    && (val != NoSpecInt))
                {
                    throw new FormatException(
                        "Day-of-Week values must be between 1 and 7");
                }
            }

            if ((incr == 0 || incr == -1) && val != AllSpecInt)
            {
                if (val != -1)
                {
                    data.Add(val);
                }
                else
                {
                    data.Add(NoSpec);
                }
                return;
            }

            int startAt = val;
            int stopAt = end;

            if (val == AllSpecInt && incr <= 0)
            {
                incr = 1;
                data.Add(AllSpec); // put in a marker, but also fill values
            }

            if (type == Second || type == Minute)
            {
                if (stopAt == -1)
                {
                    stopAt = 59;
                }
                if (startAt == -1 || startAt == AllSpecInt)
                {
                    startAt = 0;
                }
            }
            else if (type == Hour)
            {
                if (stopAt == -1)
                {
                    stopAt = 23;
                }
                if (startAt == -1 || startAt == AllSpecInt)
                {
                    startAt = 0;
                }
            }
            else if (type == DayOfMonth)
            {
                if (stopAt == -1)
                {
                    stopAt = 31;
                }
                if (startAt == -1 || startAt == AllSpecInt)
                {
                    startAt = 1;
                }
            }
            else if (type == Month)
            {
                if (stopAt == -1)
                {
                    stopAt = 12;
                }
                if (startAt == -1 || startAt == AllSpecInt)
                {
                    startAt = 1;
                }
            }
            else if (type == DayOfWeek)
            {
                if (stopAt == -1)
                {
                    stopAt = 7;
                }
                if (startAt == -1 || startAt == AllSpecInt)
                {
                    startAt = 1;
                }
            }
            else if (type == Year)
            {
                if (stopAt == -1)
                {
                    stopAt = 2099;
                }
                if (startAt == -1 || startAt == AllSpecInt)
                {
                    startAt = 1970;
                }
            }

            // if the end of the range is before the start, then we need to overflow into 
            // the next day, month etc. This is done by adding the maximum amount for that 
            // type, and using modulus max to determine the value being added.
            int max = -1;
            if (stopAt < startAt)
            {
                switch (type)
                {
                    case Second: max = 60; break;
                    case Minute: max = 60; break;
                    case Hour: max = 24; break;
                    case Month: max = 12; break;
                    case DayOfWeek: max = 7; break;
                    case DayOfMonth: max = 31; break;
                    case Year: throw new ArgumentException("Start year must be less than stop year");
                    default: throw new ArgumentException("Unexpected type encountered");
                }
                stopAt += max;
            }

            for (int i = startAt; i <= stopAt; i += incr)
            {
                if (max == -1)
                {
                    // ie: there's no max to overflow over
                    data.Add(i);
                }
                else
                {
                    // take the modulus to get the real value
                    int i2 = i % max;

                    // 1-indexed ranges should not include 0, and should include their max
                    if (i2 == 0 && (type == Month || type == DayOfWeek || type == DayOfMonth))
                    {
                        i2 = max;
                    }

                    data.Add(i2);
                }
            }
        }

		/// <summary>
		/// Gets the set of given type.
		/// </summary>
		/// <param name="type">The type of set to get.</param>
		/// <returns></returns>
        protected virtual TreeSet GetSet(int type)
        {
            switch (type)
            {
                case Second:
                    return seconds;
                case Minute:
                    return minutes;
                case Hour:
                    return hours;
                case DayOfMonth:
                    return daysOfMonth;
                case Month:
                    return months;
                case DayOfWeek:
                    return daysOfWeek;
                case Year:
                    return years;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <param name="v">The v.</param>
        /// <param name="s">The s.</param>
        /// <param name="i">The i.</param>
        /// <returns></returns>
        protected virtual ValueSet GetValue(int v, string s, int i)
        {
            char c = s[i];
            string s1 = v.ToString(CultureInfo.InvariantCulture);
            while (c >= '0' && c <= '9')
            {
                s1 += c;
                i++;
                if (i >= s.Length)
                {
                    break;
                }
                c = s[i];
            }
            ValueSet val = new ValueSet();
            if (i < s.Length)
            {
                val.pos = i;
            }
            else
            {
                val.pos = i + 1;
            }
            val.theValue = Convert.ToInt32(s1, CultureInfo.InvariantCulture);
            return val;
        }

        /// <summary>
        /// Gets the numeric value from string.
        /// </summary>
        /// <param name="s">The string to parse from.</param>
        /// <param name="i">The i.</param>
        /// <returns></returns>
        protected virtual int GetNumericValue(string s, int i)
        {
            int endOfVal = FindNextWhiteSpace(i, s);
            string val = s.Substring(i, endOfVal - i);
            return Convert.ToInt32(val, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets the month number.
        /// </summary>
        /// <param name="s">The string to map with.</param>
        /// <returns></returns>
        protected virtual int GetMonthNumber(string s)
        {
            if (monthMap.ContainsKey(s))
            {
                return (int) monthMap[s];
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// Gets the day of week number.
        /// </summary>
        /// <param name="s">The s.</param>
        /// <returns></returns>
        protected virtual int GetDayOfWeekNumber(string s)
        {
            if (dayMap.ContainsKey(s))
            {
                return (int) dayMap[s];
            }
            else
            {
                return -1;
            }
        }

		/// <summary>
		/// Gets the time from given time parts.
		/// </summary>
		/// <param name="sc">The seconds.</param>
		/// <param name="mn">The minutes.</param>
		/// <param name="hr">The hours.</param>
		/// <param name="dayofmn">The day of month.</param>
		/// <param name="mon">The month.</param>
		/// <returns></returns>
        protected virtual DateTime? GetTime(int sc, int mn, int hr, int dayofmn, int mon)
        {
            try
            {
                if (sc == -1)
                {
                    sc = 0;
                }
                if (mn == -1)
                {
                    mn = 0;
                }
                if (hr == -1)
                {
                    hr = 0;
                }
                if (dayofmn == -1)
                {
                    dayofmn = 0;
                }
                if (mon == -1)
                {
                    mon = 0;
                }
                return new DateTime(DateTime.UtcNow.Year, mon, dayofmn, hr, mn, sc);
            }
            catch (Exception)
            {
                return null;
            }
        }

		/// <summary>
		/// Gets the next fire time after the given time.
		/// </summary>
		/// <param name="afterTimeUtc">The UTC time to start searching from.</param>
		/// <returns></returns>
        public virtual DateTime? GetTimeAfter(DateTime afterTimeUtc)
        {
            // move ahead one second, since we're computing the time *after/// the
            // given time
            afterTimeUtc = afterTimeUtc.AddSeconds(1);

            // CronTrigger does not deal with milliseconds
            DateTime d = CreateDateTimeWithoutMillis(afterTimeUtc);

            // change to specified time zone

            d = TimeZone.ToLocalTime(d);


            bool gotOne = false;
            // loop until we've computed the next time, or we've past the endTime
            while (!gotOne)
            {
                ISortedSet st;
                int t;
                int sec = d.Second;

                // get second.................................................
                st = seconds.TailSet(sec);
                if (st != null && st.Count != 0)
                {
                    sec = (int) st.First();
                }
                else
                {
                    sec = ((int) seconds.First());
                    d = d.AddMinutes(1);
                }
                d = new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, sec, d.Millisecond);

                int min = d.Minute;
                int hr = d.Hour;
                t = -1;

                // get minute.................................................
                st = minutes.TailSet(min);
                if (st != null && st.Count != 0)
                {
                    t = min;
                    min = ((int) st.First());
                }
                else
                {
                    min = (int) minutes.First();
                    hr++;
                }
                if (min != t)
                {
                    d = new DateTime(d.Year, d.Month, d.Day, d.Hour, min, 0, d.Millisecond);
                    d = SetCalendarHour(d, hr);
                    continue;
                }
                d = new DateTime(d.Year, d.Month, d.Day, d.Hour, min, d.Second, d.Millisecond);

                hr = d.Hour;
                int day = d.Day;
                t = -1;

                // get hour...................................................
                st = hours.TailSet(hr);
                if (st != null && st.Count != 0)
                {
                    t = hr;
                    hr = (int) st.First();
                }
                else
                {
                    hr = (int) hours.First();
                    day++;
                }
                if (hr != t)
                {
                    int daysInMonth = DateTime.DaysInMonth(d.Year, d.Month);
                    if (day > daysInMonth)
                    {
                        d = new DateTime(d.Year, d.Month, daysInMonth, d.Hour, 0, 0, d.Millisecond).AddDays(day - daysInMonth);
                    }
                    else
                    {
                        d = new DateTime(d.Year, d.Month, day, d.Hour, 0, 0, d.Millisecond);
                    }
                    d = SetCalendarHour(d, hr);
                    continue;
                }
                d = new DateTime(d.Year, d.Month, d.Day, hr, d.Minute, d.Second, d.Millisecond);

                day = d.Day;
                int mon = d.Month;
                t = -1;
                int tmon = mon;

                // get day...................................................
                bool dayOfMSpec = !daysOfMonth.Contains(NoSpec);
                bool dayOfWSpec = !daysOfWeek.Contains(NoSpec);
                if (dayOfMSpec && !dayOfWSpec)
                {
                    // get day by day of month rule
                    st = daysOfMonth.TailSet(day);
                    if (lastdayOfMonth)
                    {
                        if (!nearestWeekday)
                        {
                            t = day;
                            day = GetLastDayOfMonth(mon, d.Year);
                        }
                        else
                        {
                            t = day;
                            day = GetLastDayOfMonth(mon, d.Year);

                            DateTime tcal = new DateTime(d.Year, mon, day, 0, 0, 0);

                            int ldom = GetLastDayOfMonth(mon, d.Year);
                            DayOfWeek dow = tcal.DayOfWeek;

                            if (dow == System.DayOfWeek.Saturday && day == 1)
                            {
                                day += 2;
                            }
                            else if (dow == System.DayOfWeek.Saturday)
                            {
                                day -= 1;
                            }
                            else if (dow == System.DayOfWeek.Sunday && day == ldom)
                            {
                                day -= 2;
                            }
                            else if (dow == System.DayOfWeek.Sunday)
                            {
                                day += 1;
                            }

                            DateTime nTime = new DateTime(tcal.Year, mon, day, hr, min, sec, d.Millisecond);
                            if (nTime.ToUniversalTime() < afterTimeUtc)
                            {
                                day = 1;
                                mon++;
                            }
                        }
                    }
                    else if (nearestWeekday)
                    {
                        t = day;
                        day = (int) daysOfMonth.First();

                        DateTime tcal = new DateTime(d.Year, mon, day, 0, 0, 0);

                        int ldom = GetLastDayOfMonth(mon, d.Year);
                        DayOfWeek dow = tcal.DayOfWeek;

                        if (dow == System.DayOfWeek.Saturday && day == 1)
                        {
                            day += 2;
                        }
                        else if (dow == System.DayOfWeek.Saturday)
                        {
                            day -= 1;
                        }
                        else if (dow == System.DayOfWeek.Sunday && day == ldom)
                        {
                            day -= 2;
                        }
                        else if (dow == System.DayOfWeek.Sunday)
                        {
                            day += 1;
                        }

                        tcal = new DateTime(tcal.Year, mon, day, hr, min, sec);
                        if (tcal.ToUniversalTime() < afterTimeUtc)
                        {
                            day = ((int) daysOfMonth.First());
                            mon++;
                        }
                    }
                    else if (st != null && st.Count != 0)
                    {
                        t = day;
                        day = (int) st.First();

                        // make sure we don't over-run a short month, such as february
                        int lastDay = GetLastDayOfMonth(mon, d.Year);
                        if (day > lastDay)
                        {
                            day = (int) daysOfMonth.First();
                            mon++;
                        }
                    }
                    else
                    {
                        day = ((int) daysOfMonth.First());
                        mon++;
                    }

                    if (day != t || mon != tmon)
                    {
                        if (mon > 12)
                        {
                            d = new DateTime(d.Year, 12, day, 0, 0, 0).AddMonths(mon - 12);
                        }
                        else
                        {
                            // This is to avoid a bug when moving from a month
                            //with 30 or 31 days to a month with less. Causes an invalid datetime to be instantiated.
                            // ex. 0 29 0 30 1 ? 2009 with clock set to 1/30/2009
                            int lDay = DateTime.DaysInMonth(d.Year, mon); 
                            if (day <= lDay)
                            {
                                d = new DateTime(d.Year, mon, day, 0, 0, 0);
                            }
                            else
                            {
                                d = new DateTime(d.Year, mon, lDay, 0, 0, 0).AddDays(day - lDay);
                            } 
                        }
                        continue;
                    }
                }
                else if (dayOfWSpec && !dayOfMSpec)
                {
                    // get day by day of week rule
                    if (lastdayOfWeek)
                    {
                        // are we looking for the last XXX day of
                        // the month?
                        int dow = ((int) daysOfWeek.First()); // desired
                        // d-o-w
                        int cDow = ((int) d.DayOfWeek) + 1; // current d-o-w
                        int daysToAdd = 0;
                        if (cDow < dow)
                        {
                            daysToAdd = dow - cDow;
                        }
                        if (cDow > dow)
                        {
                            daysToAdd = dow + (7 - cDow);
                        }

                        int lDay = GetLastDayOfMonth(mon, d.Year);

                        if (day + daysToAdd > lDay)
                        {
                            // did we already miss the
                            // last one?
                            if (mon == 12)
                            {
                                //will we pass the end of the year?
                                d = new DateTime(d.Year, mon - 11, 1, 0, 0, 0).AddYears(1);
                            }
                            else
                            {
                            	d = new DateTime(d.Year, mon + 1, 1, 0, 0, 0);
                            }
                            // we are promoting the month
                            continue;
                        }

                        // find date of last occurance of this day in this month...
                        while ((day + daysToAdd + 7) <= lDay)
                        {
                            daysToAdd += 7;
                        }

                        day += daysToAdd;

                        if (daysToAdd > 0)
                        {
                            d = new DateTime(d.Year, mon, day, 0, 0, 0);
                            // we are not promoting the month
                            continue;
                        }
                    }
                    else if (nthdayOfWeek != 0)
                    {
                        // are we looking for the Nth XXX day in the month?
                        int dow = ((int) daysOfWeek.First()); // desired
                        // d-o-w
                        int cDow = ((int) d.DayOfWeek) + 1; // current d-o-w
                        int daysToAdd = 0;
                        if (cDow < dow)
                        {
                            daysToAdd = dow - cDow;
                        }
                        else if (cDow > dow)
                        {
                            daysToAdd = dow + (7 - cDow);
                        }

                        bool dayShifted = false;
                        if (daysToAdd > 0)
                        {
                            dayShifted = true;
                        }

                        day += daysToAdd;
                        int weekOfMonth = day/7;
                        if (day%7 > 0)
                        {
                            weekOfMonth++;
                        }

                        daysToAdd = (nthdayOfWeek - weekOfMonth)*7;
                        day += daysToAdd;
                        if (daysToAdd < 0 || day > GetLastDayOfMonth(mon, d.Year))
                        {
                            if (mon == 12)
                            {
                                d = new DateTime(d.Year, mon - 11, 1, 0, 0, 0).AddYears(1);
                            }
                            else
                            {
                                d = new DateTime(d.Year, mon + 1, 1, 0, 0, 0);
                            } 
                            
                            // we are promoting the month
                            continue;
                        }
                        else if (daysToAdd > 0 || dayShifted)
                        {
                            d = new DateTime(d.Year, mon, day, 0, 0, 0);
                            // we are NOT promoting the month
                            continue;
                        }
                    }
                    else
                    {
                        int cDow = ((int) d.DayOfWeek) + 1; // current d-o-w
                        int dow = ((int) daysOfWeek.First()); // desired
                        // d-o-w
                        st = daysOfWeek.TailSet(cDow);
                        if (st != null && st.Count > 0)
                        {
                            dow = ((int) st.First());
                        }

                        int daysToAdd = 0;
                        if (cDow < dow)
                        {
                            daysToAdd = dow - cDow;
                        }
                        if (cDow > dow)
                        {
                            daysToAdd = dow + (7 - cDow);
                        }

                        int lDay = GetLastDayOfMonth(mon, d.Year);

                        if (day + daysToAdd > lDay)
                        {
                            // will we pass the end of the month?

                            if (mon == 12)
                            {
                                //will we pass the end of the year?
                                d = new DateTime(d.Year, mon - 11, 1, 0, 0, 0).AddYears(1);
                            }
                            else
                            {
                            	d = new DateTime(d.Year, mon + 1, 1, 0, 0, 0);
                            }
                            // we are promoting the month
                            continue;
                        }
                        else if (daysToAdd > 0)
                        {
                            // are we swithing days?
                            d = new DateTime(d.Year, mon, day + daysToAdd, 0, 0, 0);
                            continue;
                        }
                    }
                }
                else
                {
                    // dayOfWSpec && !dayOfMSpec
                    throw new Exception(
                        "Support for specifying both a day-of-week AND a day-of-month parameter is not implemented.");
                }

                d = new DateTime(d.Year, d.Month, day, d.Hour, d.Minute, d.Second);
                mon = d.Month;
                int year = d.Year;
                t = -1;

                // test for expressions that never generate a valid fire date,
                // but keep looping...
                if (year > 2099)
                {
                    return null;
                }

                // get month...................................................
                st = months.TailSet((mon));
                if (st != null && st.Count != 0)
                {
                    t = mon;
                    mon = ((int) st.First());
                }
                else
                {
                    mon = ((int) months.First());
                    year++;
                }
                if (mon != t)
                {
                    d = new DateTime(year, mon, 1, 0, 0, 0);
                    continue;
                }
                d = new DateTime(d.Year, mon, d.Day, d.Hour, d.Minute, d.Second);
                year = d.Year;
                t = -1;

                // get year...................................................
                st = years.TailSet((year));
                if (st != null && st.Count != 0)
                {
                    t = year;
                    year = ((int) st.First());
                }
                else
                {
                    return null;
                } // ran out of years...

                if (year != t)
                {
                    d = new DateTime(year, 1, 1, 0, 0, 0);
                    continue;
                }
                d = new DateTime(year, d.Month, d.Day, d.Hour, d.Minute, d.Second);

                gotOne = true;
            } // while( !done )
            return TimeZone.ToUniversalTime(d);
        }

        /// <summary>
        /// Creates the date time without milliseconds.
        /// </summary>
        /// <param name="time">The time.</param>
        /// <returns></returns>
        protected static DateTime CreateDateTimeWithoutMillis(DateTime time)
        {
            return new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second);
        }


        /// <summary>
        /// Advance the calendar to the particular hour paying particular attention
        /// to daylight saving problems.
        /// </summary>
        /// <param name="date">The date.</param>
        /// <param name="hour">The hour.</param>
        /// <returns></returns>
        protected static DateTime SetCalendarHour(DateTime date, int hour)
        {
            // Java version of Quartz uses lenient calendar
            // so hour 24 creates day increment and zeroes hour
            int hourToSet = hour;
            if (hourToSet == 24)
            {
                hourToSet = 0;
            }
            DateTime d =
                new DateTime(date.Year, date.Month, date.Day, hourToSet, date.Minute, date.Second, date.Millisecond);
            if (hour == 24)
            {
                // inrement day
                d = d.AddDays(1);
            }
            return d;
        }

        /// <summary>
        /// Gets the time before.
        /// </summary>
        /// <param name="endTime">The end time.</param>
        /// <returns></returns>
        public virtual DateTime? GetTimeBefore(DateTime? endTime)
        {
            // TODO: implement
            return null;
        }

        /// <summary>
        /// NOT YET IMPLEMENTED: Returns the final time that the 
        /// <see cref="CronExpression" /> will match.
        /// </summary>
        /// <returns></returns>
        public virtual DateTime? GetFinalFireTime()
        {
            // TODO: implement QUARTZ-423
            return null;
        }

        /// <summary>
        /// Determines whether given year is a leap year.
        /// </summary>
        /// <param name="year">The year.</param>
        /// <returns>
        /// 	<c>true</c> if the specified year is a leap year; otherwise, <c>false</c>.
        /// </returns>
        protected virtual bool IsLeapYear(int year)
        {
            return DateTime.IsLeapYear(year);
        }

        /// <summary>
        /// Gets the last day of month.
        /// </summary>
        /// <param name="monthNum">The month num.</param>
        /// <param name="year">The year.</param>
        /// <returns></returns>
        protected virtual int GetLastDayOfMonth(int monthNum, int year)
        {
            return DateTime.DaysInMonth(year, monthNum);
        }

        /// <summary>
        /// Creates a new object that is a copy of the current instance.
        /// </summary>
        /// <returns>
        /// A new object that is a copy of this instance.
        /// </returns>
        public object Clone()
        {
            CronExpression copy;
            try
            {
                copy = new CronExpression(CronExpressionString);
                copy.TimeZone = TimeZone;
            }
            catch (FormatException)
            {
                // never happens since the source is valid...
                throw new Exception("Not Cloneable.");
            }
            return copy;
        }

        public void OnDeserialization(object sender)
        {
            BuildExpression(cronExpressionString);
        }
    }

	/// <summary>
	/// Helper class for cron expression handling.
	/// </summary>
    public class ValueSet
    {
		/// <summary>
		/// The value.
		/// </summary>
        public int theValue;

		/// <summary>
		/// The position.
		/// </summary>
		public int pos;
    }
}
