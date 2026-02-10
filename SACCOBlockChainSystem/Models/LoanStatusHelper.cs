namespace SACCOBlockChainSystem.Helpers
{
    public static class LoanStatusHelper
    {
        private static readonly Dictionary<int, string> StatusMap = new()
        {
            { 1, "Application" },
            { 2, "Guarantors" },
            { 3, "Appraisal" },
            { 4, "Endorsement" },
            { 5, "Approved" },
            { 6, "Disbursed" },
            { 7, "Rejected" },
            { 8, "Closed" }
        };

        private static readonly Dictionary<int, List<int>> AllowedTransitions = new()
        {
            { 1, new List<int> { 2, 7 } }, // Application -> Guarantors or Rejected
            { 2, new List<int> { 3, 1 } }, // Guarantors -> Appraisal or back to Application
            { 3, new List<int> { 4, 2 } }, // Appraisal -> Endorsement or back to Guarantors
            { 4, new List<int> { 5, 3 } }, // Endorsement -> Approved or back to Appraisal
            { 5, new List<int> { 6, 8 } }, // Approved -> Disbursed or Closed
            { 6, new List<int> { 8 } },    // Disbursed -> Closed
            { 7, new List<int> { } },      // Rejected (terminal)
            { 8, new List<int> { } }       // Closed (terminal)
        };

        public static string GetStatusDescription(int status)
        {
            return StatusMap.ContainsKey(status) ? StatusMap[status] : "Unknown";
        }

        public static bool IsValidTransition(int currentStatus, int newStatus)
        {
            if (!AllowedTransitions.ContainsKey(currentStatus))
                return false;

            return AllowedTransitions[currentStatus].Contains(newStatus);
        }

        public static List<int> GetNextAllowedStatuses(int currentStatus)
        {
            return AllowedTransitions.ContainsKey(currentStatus)
                ? AllowedTransitions[currentStatus]
                : new List<int>();
        }

        public static bool IsTerminalStatus(int status)
        {
            return status == 7 || status == 8; // Rejected or Closed
        }

        public static bool CanBeModified(int status)
        {
            // Only applications in early stages can be modified
            return status == 1 || status == 2 || status == 3;
        }
    }
}