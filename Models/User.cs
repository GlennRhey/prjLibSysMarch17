namespace prjLibrarySystem.Models
{
    // Mirrors tblUsers: UserID, PasswordHash, Role, FullName, Email, IsActive, CreatedAt
    // FullName is only populated for Admins — Students use tblMembers.FullName
    public class User
    {
        public string UserID { get; set; }  // e.g. EMP-001 (Admin) or 2023-0001 (Student)
        public string Role { get; set; }  // 'Admin' or 'Student'
        public string FullName { get; set; }
        public string Email { get; set; }

        public bool IsAdmin => Role == "Admin";
        public bool IsStudent => Role == "Student";
    }
}