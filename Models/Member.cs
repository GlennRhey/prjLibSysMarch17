namespace prjLibrarySystem.Models
{
    // Mirrors tblMembers: MemberID, UserID, FullName, Course, YearLevel
    public class Member
    {
        public int MemberID { get; set; }
        public string UserID { get; set; }  // FK to tblUsers.UserID
        public string FullName { get; set; }
        public string Course { get; set; }  // e.g. BSIT, BSCS
        public int YearLevel { get; set; }  // 1–4

        // Joined from tblUsers when needed
        public string Email { get; set; }
        public bool IsActive { get; set; }
    }
}