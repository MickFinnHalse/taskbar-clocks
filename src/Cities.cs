using System;
using System.Collections.Generic;

namespace TaskbarClocks
{
    public sealed class City
    {
        public string Name    { get; private set; }
        public string Country { get; private set; }
        public string ZoneId  { get; private set; }   // Windows id, read by TimeZoneInfo
        public string Iana    { get; private set; }   // tz database id, kept to verify the mapping

        /// <summary>
        /// zoneIds is "preferred;fallback": the first Windows zone this PC actually has.
        /// Lets a city move to a zone Windows Update added recently without breaking on a
        /// PC that hasn't had that update yet.
        /// </summary>
        public City(string name, string country, string zoneIds, string iana)
        {
            Name = name; Country = country; Iana = iana;
            string[] ids = zoneIds.Split(';');
            ZoneId = ids[ids.Length - 1];
            foreach (string id in ids)
                if (Zones.Exists(id)) { ZoneId = id; break; }
        }

        public string Display { get { return Name + ", " + Country; } }
    }

    public static class Zones
    {
        public static bool Exists(string id)
        {
            try { TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
            catch { return false; }
        }
    }

    /// <summary>
    /// Cities offered when adding a clock. Each maps to a Windows time zone, and Windows
    /// holds the daylight-saving rules for that zone, kept current by Windows Update. A
    /// rule change inside an existing zone needs nothing here. A place that SPLITS off into
    /// a new zone does: in 2026 British Columbia, Alberta and Manitoba stopped changing
    /// their clocks, Windows gave each its own zone, and the entries below had to move.
    /// A city not listed here can still be found with Search or picked by time zone.
    ///
    /// ASCII only: csc reads a BOM-less source file in the system code page.
    /// </summary>
    public static class Cities
    {
        public static readonly City[] All = Build(new[]
        {
            // Southern Africa
            "Johannesburg|South Africa|South Africa Standard Time|Africa/Johannesburg",
            "Cape Town|South Africa|South Africa Standard Time|Africa/Johannesburg",
            "Durban|South Africa|South Africa Standard Time|Africa/Johannesburg",
            "Pretoria|South Africa|South Africa Standard Time|Africa/Johannesburg",
            "Gqeberha|South Africa|South Africa Standard Time|Africa/Johannesburg",
            "Port Elizabeth|South Africa|South Africa Standard Time|Africa/Johannesburg",
            "Bloemfontein|South Africa|South Africa Standard Time|Africa/Johannesburg",
            "Harare|Zimbabwe|South Africa Standard Time|Africa/Harare",
            "Gaborone|Botswana|South Africa Standard Time|Africa/Gaborone",
            "Maputo|Mozambique|South Africa Standard Time|Africa/Maputo",
            "Lusaka|Zambia|South Africa Standard Time|Africa/Lusaka",
            "Lilongwe|Malawi|South Africa Standard Time|Africa/Blantyre",
            "Kigali|Rwanda|South Africa Standard Time|Africa/Kigali",
            "Maseru|Lesotho|South Africa Standard Time|Africa/Maseru",
            "Mbabane|Eswatini|South Africa Standard Time|Africa/Mbabane",
            "Windhoek|Namibia|Namibia Standard Time|Africa/Windhoek",
            "Port Louis|Mauritius|Mauritius Standard Time|Indian/Mauritius",

            // Rest of Africa
            "Nairobi|Kenya|E. Africa Standard Time|Africa/Nairobi",
            "Kampala|Uganda|E. Africa Standard Time|Africa/Kampala",
            "Dar es Salaam|Tanzania|E. Africa Standard Time|Africa/Dar_es_Salaam",
            "Addis Ababa|Ethiopia|E. Africa Standard Time|Africa/Addis_Ababa",
            "Lagos|Nigeria|W. Central Africa Standard Time|Africa/Lagos",
            "Abuja|Nigeria|W. Central Africa Standard Time|Africa/Lagos",
            "Kinshasa|DR Congo|W. Central Africa Standard Time|Africa/Kinshasa",
            "Luanda|Angola|W. Central Africa Standard Time|Africa/Luanda",
            "Tunis|Tunisia|W. Central Africa Standard Time|Africa/Tunis",
            "Algiers|Algeria|W. Central Africa Standard Time|Africa/Algiers",
            "Accra|Ghana|Greenwich Standard Time|Africa/Accra",
            "Dakar|Senegal|Greenwich Standard Time|Africa/Dakar",
            "Cairo|Egypt|Egypt Standard Time|Africa/Cairo",
            "Casablanca|Morocco|Morocco Standard Time|Africa/Casablanca",

            // Europe
            "London|United Kingdom|GMT Standard Time|Europe/London",
            "Manchester|United Kingdom|GMT Standard Time|Europe/London",
            "Edinburgh|United Kingdom|GMT Standard Time|Europe/London",
            "Dublin|Ireland|GMT Standard Time|Europe/Dublin",
            "Lisbon|Portugal|GMT Standard Time|Europe/Lisbon",
            "Reykjavik|Iceland|Greenwich Standard Time|Atlantic/Reykjavik",
            "Paris|France|Romance Standard Time|Europe/Paris",
            "Brussels|Belgium|Romance Standard Time|Europe/Brussels",
            "Madrid|Spain|Romance Standard Time|Europe/Madrid",
            "Barcelona|Spain|Romance Standard Time|Europe/Madrid",
            "Copenhagen|Denmark|Romance Standard Time|Europe/Copenhagen",
            "Amsterdam|Netherlands|W. Europe Standard Time|Europe/Amsterdam",
            "Berlin|Germany|W. Europe Standard Time|Europe/Berlin",
            "Munich|Germany|W. Europe Standard Time|Europe/Berlin",
            "Frankfurt|Germany|W. Europe Standard Time|Europe/Berlin",
            "Rome|Italy|W. Europe Standard Time|Europe/Rome",
            "Milan|Italy|W. Europe Standard Time|Europe/Rome",
            "Vienna|Austria|W. Europe Standard Time|Europe/Vienna",
            "Zurich|Switzerland|W. Europe Standard Time|Europe/Zurich",
            "Geneva|Switzerland|W. Europe Standard Time|Europe/Zurich",
            "Stockholm|Sweden|W. Europe Standard Time|Europe/Stockholm",
            "Oslo|Norway|W. Europe Standard Time|Europe/Oslo",
            "Luxembourg|Luxembourg|W. Europe Standard Time|Europe/Luxembourg",
            "Valletta|Malta|W. Europe Standard Time|Europe/Malta",
            "Prague|Czechia|Central Europe Standard Time|Europe/Prague",
            "Budapest|Hungary|Central Europe Standard Time|Europe/Budapest",
            "Bratislava|Slovakia|Central Europe Standard Time|Europe/Bratislava",
            "Ljubljana|Slovenia|Central Europe Standard Time|Europe/Ljubljana",
            "Belgrade|Serbia|Central Europe Standard Time|Europe/Belgrade",
            "Warsaw|Poland|Central European Standard Time|Europe/Warsaw",
            "Zagreb|Croatia|Central European Standard Time|Europe/Zagreb",
            "Athens|Greece|GTB Standard Time|Europe/Athens",
            "Bucharest|Romania|GTB Standard Time|Europe/Bucharest",
            "Nicosia|Cyprus|GTB Standard Time|Asia/Nicosia",
            "Helsinki|Finland|FLE Standard Time|Europe/Helsinki",
            "Kyiv|Ukraine|FLE Standard Time|Europe/Kyiv",
            "Riga|Latvia|FLE Standard Time|Europe/Riga",
            "Tallinn|Estonia|FLE Standard Time|Europe/Tallinn",
            "Vilnius|Lithuania|FLE Standard Time|Europe/Vilnius",
            "Sofia|Bulgaria|FLE Standard Time|Europe/Sofia",
            "Istanbul|Turkey|Turkey Standard Time|Europe/Istanbul",
            "Moscow|Russia|Russian Standard Time|Europe/Moscow",

            // Middle East
            "Tel Aviv|Israel|Israel Standard Time|Asia/Jerusalem",
            "Jerusalem|Israel|Israel Standard Time|Asia/Jerusalem",
            "Amman|Jordan|Jordan Standard Time|Asia/Amman",
            "Beirut|Lebanon|Middle East Standard Time|Asia/Beirut",
            "Baghdad|Iraq|Arabic Standard Time|Asia/Baghdad",
            "Riyadh|Saudi Arabia|Arab Standard Time|Asia/Riyadh",
            "Kuwait City|Kuwait|Arab Standard Time|Asia/Kuwait",
            "Doha|Qatar|Arab Standard Time|Asia/Qatar",
            "Manama|Bahrain|Arab Standard Time|Asia/Bahrain",
            "Dubai|United Arab Emirates|Arabian Standard Time|Asia/Dubai",
            "Abu Dhabi|United Arab Emirates|Arabian Standard Time|Asia/Dubai",
            "Muscat|Oman|Arabian Standard Time|Asia/Muscat",
            "Tehran|Iran|Iran Standard Time|Asia/Tehran",

            // Asia
            "Kabul|Afghanistan|Afghanistan Standard Time|Asia/Kabul",
            "Karachi|Pakistan|Pakistan Standard Time|Asia/Karachi",
            "Lahore|Pakistan|Pakistan Standard Time|Asia/Karachi",
            "Islamabad|Pakistan|Pakistan Standard Time|Asia/Karachi",
            "Tashkent|Uzbekistan|West Asia Standard Time|Asia/Tashkent",
            "Mumbai|India|India Standard Time|Asia/Kolkata",
            "New Delhi|India|India Standard Time|Asia/Kolkata",
            "Delhi|India|India Standard Time|Asia/Kolkata",
            "Bengaluru|India|India Standard Time|Asia/Kolkata",
            "Bangalore|India|India Standard Time|Asia/Kolkata",
            "Chennai|India|India Standard Time|Asia/Kolkata",
            "Hyderabad|India|India Standard Time|Asia/Kolkata",
            "Kolkata|India|India Standard Time|Asia/Kolkata",
            "Colombo|Sri Lanka|Sri Lanka Standard Time|Asia/Colombo",
            "Kathmandu|Nepal|Nepal Standard Time|Asia/Kathmandu",
            "Dhaka|Bangladesh|Bangladesh Standard Time|Asia/Dhaka",
            "Yangon|Myanmar|Myanmar Standard Time|Asia/Yangon",
            "Bangkok|Thailand|SE Asia Standard Time|Asia/Bangkok",
            "Jakarta|Indonesia|SE Asia Standard Time|Asia/Jakarta",
            "Hanoi|Vietnam|SE Asia Standard Time|Asia/Ho_Chi_Minh",
            "Ho Chi Minh City|Vietnam|SE Asia Standard Time|Asia/Ho_Chi_Minh",
            "Phnom Penh|Cambodia|SE Asia Standard Time|Asia/Phnom_Penh",
            "Kuala Lumpur|Malaysia|Singapore Standard Time|Asia/Kuala_Lumpur",
            "Singapore|Singapore|Singapore Standard Time|Asia/Singapore",
            "Manila|Philippines|Singapore Standard Time|Asia/Manila",
            "Denpasar (Bali)|Indonesia|Singapore Standard Time|Asia/Makassar",
            "Hong Kong|China|China Standard Time|Asia/Hong_Kong",
            "Beijing|China|China Standard Time|Asia/Shanghai",
            "Shanghai|China|China Standard Time|Asia/Shanghai",
            "Shenzhen|China|China Standard Time|Asia/Shanghai",
            "Guangzhou|China|China Standard Time|Asia/Shanghai",
            "Taipei|Taiwan|Taipei Standard Time|Asia/Taipei",
            "Seoul|South Korea|Korea Standard Time|Asia/Seoul",
            "Tokyo|Japan|Tokyo Standard Time|Asia/Tokyo",
            "Osaka|Japan|Tokyo Standard Time|Asia/Tokyo",

            // Australia and the Pacific
            "Perth|Australia|W. Australia Standard Time|Australia/Perth",
            "Darwin|Australia|AUS Central Standard Time|Australia/Darwin",
            "Adelaide|Australia|Cen. Australia Standard Time|Australia/Adelaide",
            "Brisbane|Australia|E. Australia Standard Time|Australia/Brisbane",
            "Sydney|Australia|AUS Eastern Standard Time|Australia/Sydney",
            "Melbourne|Australia|AUS Eastern Standard Time|Australia/Melbourne",
            "Canberra|Australia|AUS Eastern Standard Time|Australia/Sydney",
            "Hobart|Australia|Tasmania Standard Time|Australia/Hobart",
            "Port Moresby|Papua New Guinea|West Pacific Standard Time|Pacific/Port_Moresby",
            "Auckland|New Zealand|New Zealand Standard Time|Pacific/Auckland",
            "Wellington|New Zealand|New Zealand Standard Time|Pacific/Auckland",
            "Christchurch|New Zealand|New Zealand Standard Time|Pacific/Auckland",
            "Suva|Fiji|Fiji Standard Time|Pacific/Fiji",
            "Honolulu|United States|Hawaiian Standard Time|Pacific/Honolulu",

            // North America
            "Anchorage|United States|Alaskan Standard Time|America/Anchorage",
            "Los Angeles|United States|Pacific Standard Time|America/Los_Angeles",
            "San Francisco|United States|Pacific Standard Time|America/Los_Angeles",
            "San Diego|United States|Pacific Standard Time|America/Los_Angeles",
            "Seattle|United States|Pacific Standard Time|America/Los_Angeles",
            "Portland|United States|Pacific Standard Time|America/Los_Angeles",
            "Las Vegas|United States|Pacific Standard Time|America/Los_Angeles",
            "Vancouver|Canada|British Columbia Standard Time;Pacific Standard Time|America/Vancouver",
            "Tijuana|Mexico|Pacific Standard Time (Mexico)|America/Tijuana",
            "Phoenix|United States|US Mountain Standard Time|America/Phoenix",
            "Denver|United States|Mountain Standard Time|America/Denver",
            "Salt Lake City|United States|Mountain Standard Time|America/Denver",
            "Calgary|Canada|Alberta Standard Time;Mountain Standard Time|America/Edmonton",
            "Edmonton|Canada|Alberta Standard Time;Mountain Standard Time|America/Edmonton",
            "Chicago|United States|Central Standard Time|America/Chicago",
            "Houston|United States|Central Standard Time|America/Chicago",
            "Dallas|United States|Central Standard Time|America/Chicago",
            "Austin|United States|Central Standard Time|America/Chicago",
            "Minneapolis|United States|Central Standard Time|America/Chicago",
            "New Orleans|United States|Central Standard Time|America/Chicago",
            "Winnipeg|Canada|Manitoba Standard Time;Central Standard Time|America/Winnipeg",
            "Regina|Canada|Canada Central Standard Time|America/Regina",
            "Mexico City|Mexico|Central Standard Time (Mexico)|America/Mexico_City",
            "Guadalajara|Mexico|Central Standard Time (Mexico)|America/Mexico_City",
            "Guatemala City|Guatemala|Central America Standard Time|America/Guatemala",
            "San Jose|Costa Rica|Central America Standard Time|America/Costa_Rica",
            "New York|United States|Eastern Standard Time|America/New_York",
            "Washington DC|United States|Eastern Standard Time|America/New_York",
            "Boston|United States|Eastern Standard Time|America/New_York",
            "Philadelphia|United States|Eastern Standard Time|America/New_York",
            "Atlanta|United States|Eastern Standard Time|America/New_York",
            "Miami|United States|Eastern Standard Time|America/New_York",
            "Detroit|United States|Eastern Standard Time|America/Detroit",
            "Toronto|Canada|Eastern Standard Time|America/Toronto",
            "Ottawa|Canada|Eastern Standard Time|America/Toronto",
            "Montreal|Canada|Eastern Standard Time|America/Toronto",
            "Halifax|Canada|Atlantic Standard Time|America/Halifax",
            "St John's|Canada|Newfoundland Standard Time|America/St_Johns",
            "Havana|Cuba|Cuba Standard Time|America/Havana",
            "Kingston|Jamaica|SA Pacific Standard Time|America/Jamaica",
            "Panama City|Panama|SA Pacific Standard Time|America/Panama",
            "San Juan|Puerto Rico|SA Western Standard Time|America/Puerto_Rico",
            "Santo Domingo|Dominican Republic|SA Western Standard Time|America/Santo_Domingo",

            // South America
            "Bogota|Colombia|SA Pacific Standard Time|America/Bogota",
            "Lima|Peru|SA Pacific Standard Time|America/Lima",
            "Quito|Ecuador|SA Pacific Standard Time|America/Guayaquil",
            "Caracas|Venezuela|Venezuela Standard Time|America/Caracas",
            "La Paz|Bolivia|SA Western Standard Time|America/La_Paz",
            "Santiago|Chile|Pacific SA Standard Time|America/Santiago",
            "Buenos Aires|Argentina|Argentina Standard Time|America/Argentina/Buenos_Aires",
            "Montevideo|Uruguay|Montevideo Standard Time|America/Montevideo",
            "Asuncion|Paraguay|Paraguay Standard Time|America/Asuncion",
            "Sao Paulo|Brazil|E. South America Standard Time|America/Sao_Paulo",
            "Rio de Janeiro|Brazil|E. South America Standard Time|America/Sao_Paulo",
            "Brasilia|Brazil|E. South America Standard Time|America/Sao_Paulo",

            "UTC|Coordinated Universal Time|UTC|Etc/UTC"
        });

        private static City[] Build(string[] rows)
        {
            var list = new List<City>(rows.Length);
            foreach (string row in rows)
            {
                string[] p = row.Split('|');
                list.Add(new City(p[0], p[1], p[2], p[3]));
            }
            return list.ToArray();
        }

        /// <summary>
        /// The city whose "Name, Country" or bare name matches what was typed, ignoring
        /// case and stray spaces. Null when there is no match.
        /// </summary>
        public static City Find(string typed)
        {
            string t = Squash(typed);
            if (t.Length == 0) return null;
            foreach (City c in All)
                if (Squash(c.Display) == t) return c;
            foreach (City c in All)
                if (Squash(c.Name) == t) return c;
            return null;
        }

        private static string Squash(string s)
        {
            if (s == null) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char ch in s.ToLowerInvariant())
                if (!char.IsWhiteSpace(ch)) sb.Append(ch);
            return sb.ToString();
        }
    }
}
