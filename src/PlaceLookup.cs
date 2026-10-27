using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

namespace TaskbarClocks
{
    /// <summary>A town or city found online, with the Windows time zone it uses.</summary>
    public sealed class Place
    {
        public string Name    { get; set; }
        public string Region  { get; set; }   // state, province
        public string Country { get; set; }
        public string ZoneId  { get; set; }   // Windows id

        public string Display
        {
            get
            {
                var parts = new List<string> { Name };
                if (!string.IsNullOrEmpty(Region) && Region != Name) parts.Add(Region);
                if (!string.IsNullOrEmpty(Country)) parts.Add(Country);
                return string.Join(", ", parts.ToArray());
            }
        }

        public override string ToString() { return Display; }   // what the results list shows
    }

    /// <summary>
    /// Looks a place name up with Open-Meteo's free geocoding service (no key, no
    /// account), which knows small towns and returns each one's tz database zone.
    /// Windows' own ICU library then turns that zone into the Windows id the clocks run
    /// on, so no mapping table has to be kept up to date here.
    /// </summary>
    public static class PlaceLookup
    {
        private const string Endpoint = "https://geocoding-api.open-meteo.com/v1/search";

        /// <summary>
        /// Searches in the background and calls back on the UI thread with the places
        /// found (towns and cities only - not airports or landmarks), or an error message.
        /// </summary>
        public static void Search(string name, Action<List<Place>, string> done)
        {
            // Built against .NET 4.0, so TLS 1.2 is not on by default; the service needs it.
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;

            var client = new WebClient { Encoding = Encoding.UTF8 };
            client.Headers[HttpRequestHeader.UserAgent] = "TaskbarClocks";
            client.DownloadStringCompleted += (s, e) =>
            {
                client.Dispose();
                if (e.Error != null)
                {
                    done(new List<Place>(), "Couldn't reach the place search - are you offline?");
                    return;
                }
                try
                {
                    int unplaceable;
                    List<Place> found = Parse(e.Result, out unplaceable);
                    // Saying "nothing found" when the town WAS found but its zone could
                    // not be matched would send the user looking for a typo.
                    done(found, found.Count == 0 && unplaceable > 0
                                ? "Found it, but this PC can't match its time zone."
                                : null);
                }
                catch { done(new List<Place>(), "The place search sent back something unreadable."); }
            };
            string url = Endpoint + "?name=" + Uri.EscapeDataString(name.Trim())
                       + "&count=20&language=en&format=json";
            client.DownloadStringAsync(new Uri(url));
        }

        private static List<Place> Parse(string json, out int unplaceable)
        {
            var found = new List<Place>();
            unplaceable = 0;
            var root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            object raw;
            if (root == null || !root.TryGetValue("results", out raw) || !(raw is IEnumerable)) return found;

            var seen = new HashSet<string>();
            foreach (object item in (IEnumerable)raw)
            {
                var r = item as Dictionary<string, object>;
                if (r == null) continue;

                // GeoNames feature codes: PPL* are populated places. Skip the airports,
                // parks and historical markers that share a town's name.
                string code = Str(r, "feature_code");
                if (!code.StartsWith("PPL", StringComparison.Ordinal)) continue;

                string zone = WindowsZoneFor(Str(r, "timezone"));
                if (zone == null) { unplaceable++; continue; }

                var p = new Place
                {
                    Name    = Str(r, "name"),
                    Region  = Str(r, "admin1"),
                    Country = Str(r, "country"),
                    ZoneId  = zone
                };
                if (p.Name.Length > 0 && seen.Add(p.Display)) found.Add(p);
            }
            return found;
        }

        private static string Str(Dictionary<string, object> r, string key)
        {
            object v;
            return r.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }

        [DllImport("icu.dll", CharSet = CharSet.Unicode)]
        private static extern int ucal_getWindowsTimeZoneID(string id, int len, StringBuilder winid,
                                                            int capacity, ref int status);

        /// <summary>
        /// Where the icu.dll that ships with Windows lags Windows' own time zone list.
        /// ICU 72 (Windows 11, 2026) still maps these places to zones that change their
        /// clocks, while Windows Update has since added zones for the new rules:
        /// British Columbia, Alberta (with the Northwest Territories) and Manitoba stopped
        /// changing in 2026 (tzdata 2026b-e); Ciudad Juarez has followed US Mountain time
        /// since 2022, and ICU has no mapping for it at all. Each override is used only if
        /// this PC has the zone; otherwise ICU's answer stands.
        /// </summary>
        private static readonly Dictionary<string, string> Overrides = new Dictionary<string, string>
        {
            { "America/Vancouver",     "British Columbia Standard Time" },
            { "America/Edmonton",      "Alberta Standard Time" },
            { "America/Yellowknife",   "Alberta Standard Time" },
            { "America/Inuvik",        "Alberta Standard Time" },
            { "America/Winnipeg",      "Manitoba Standard Time" },
            { "America/Ciudad_Juarez", "Mountain Standard Time" }
        };

        /// <summary>
        /// "America/Toronto" -> "Eastern Standard Time", or null when Windows does not
        /// know the zone. icu.dll ships with Windows 10 1903 and later.
        /// </summary>
        public static string WindowsZoneFor(string iana)
        {
            if (string.IsNullOrEmpty(iana)) return null;
            string preferred;
            if (Overrides.TryGetValue(iana, out preferred) && Zones.Exists(preferred)) return preferred;
            try
            {
                var sb = new StringBuilder(128);
                int status = 0;
                int n = ucal_getWindowsTimeZoneID(iana, iana.Length, sb, sb.Capacity, ref status);
                if (status > 0 || n <= 0) return null;   // ICU errors are positive
                string id = sb.ToString(0, n);
                TimeZoneInfo.FindSystemTimeZoneById(id);   // throws if not on this PC
                return id;
            }
            catch { return null; }
        }
    }
}
