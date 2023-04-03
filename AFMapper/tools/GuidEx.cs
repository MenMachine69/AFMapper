namespace CR3.CORE
{
    /// <summary>
    /// Extension methods for System.Guid
    /// </summary>
    public static class GuidEx
    {
        /// <summary>
        /// Checks if a Guid is empty (equal to Guid.Empty)
        /// </summary>
        /// <param name="guid">Guid to be checked</param>
        /// <returns>true if the Guid is equal to Guid.Empty</returns>
        public static bool IsEmpty(this Guid guid) { return guid.Equals(Guid.Empty); }

        /// <summary>
        /// Checks if a Guid is not empty (not equal to Guid.Empty)
        /// </summary>
        /// <param name="guid">Guid to be checked</param>
        /// <returns>true if the Guid is not equal to Guid.Empty</returns>
        public static bool IsNotEmpty(this Guid guid) { return !guid.Equals(Guid.Empty); }
    }

}
