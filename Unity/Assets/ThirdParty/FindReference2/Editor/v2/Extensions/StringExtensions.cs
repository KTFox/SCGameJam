namespace vietlabs.fr2
{
    internal static class StringExtensions
    {
        internal static bool IsHexChar(this char c)
        {
            return ((c >= '0') && (c <= '9')) || ((c >= 'a') && (c <= 'f')) || ((c >= 'A') && (c <= 'F'));
        }

        internal static bool IsValidHexString(this string str)
        {
            foreach (char c in str)
            {
                if (!c.IsHexChar()) return false;
            }

            return true;
        }
    }
}