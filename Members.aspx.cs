using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using prjLibrarySystem.Models;

namespace prjLibrarySystem
{
    public partial class Members : System.Web.UI.Page
    {
        private string SearchTerm
        {
            get { return ViewState["SearchTerm"] as string ?? ""; }
            set { ViewState["SearchTerm"] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["UserID"] == null)
            {
                Response.Redirect("Login.aspx");
                return;
            }

            string role = Session["Role"]?.ToString();
            if (role != "Admin" && role != "Super Admin")
            {
                Response.Redirect("StudentDashboard.aspx");
                return;
            }

            // Show the Super Admin nav link only for Super Admins
            // (no longer needed — Super Admin has their own dashboard)

            if (!IsPostBack) LoadMembers();
        }

        // ── Load: Admin view shows ONLY Students and Teachers ─────────────────

        private void LoadMembers()
        {
            try
            {
                var parameters = new List<SqlParameter>();

                // Base query — always restricted to tblMembers (Students + Teachers)
                string query = @"
                    SELECT
                        u.UserID        AS MemberID,
                        m.FullName,
                        u.UserID        AS Username,
                        u.Email,
                        m.Course,
                        m.YearLevel,
                        m.MemberType    AS Role,
                        u.CreatedAt     AS RegistrationDate,
                        u.IsActive,
                        CASE WHEN u.IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS Status
                    FROM tblMembers m
                    INNER JOIN tblUsers u ON m.UserID = u.UserID
                    WHERE u.Role = 'Member'";

                // Optional MemberType sub-filter (Student or Teacher)
                if (!string.IsNullOrEmpty(ddlMembershipType.SelectedValue))
                {
                    query += " AND m.MemberType = @MemberType";
                    parameters.Add(new SqlParameter("@MemberType", ddlMembershipType.SelectedValue));
                }

                // Search filter
                if (!string.IsNullOrEmpty(SearchTerm))
                {
                    query += " AND (m.FullName LIKE @Search OR u.Email LIKE @Search OR u.UserID LIKE @Search)";
                    parameters.Add(new SqlParameter("@Search", "%" + SearchTerm + "%"));
                }

                // Status filter
                if (!string.IsNullOrEmpty(ddlStatus.SelectedValue))
                {
                    query += " AND (CASE WHEN u.IsActive = 1 THEN 'Active' ELSE 'Inactive' END) = @Status";
                    parameters.Add(new SqlParameter("@Status", ddlStatus.SelectedValue));
                }

                query += " ORDER BY m.MemberType ASC, u.UserID ASC";

                DataTable dt = DatabaseHelper.ExecuteQuery(query, parameters.ToArray());
                gvMembers.DataSource = dt;
                gvMembers.DataBind();
                txtSearchMember.Text = SearchTerm;
            }
            catch (Exception ex)
            {
                gvMembers.DataSource = null;
                gvMembers.DataBind();
                ScriptManager.RegisterStartupScript(this, GetType(), "error",
                    $"alert('Error loading members: {ex.Message}');", true);
            }
        }

        // ── Save (Add / Edit) ─────────────────────────────────────────────────

        protected void btnSaveMember_Click(object sender, EventArgs e)
        {
            try
            {
                // hfSelectedRole is either "Student" or "Teacher" — never "Admin" here
                string selectedRole = hfSelectedRole.Value;
                if (selectedRole != "Student" && selectedRole != "Teacher")
                    selectedRole = "Student";

                string adminId = Session["UserID"]?.ToString() ?? "";
                string adminName = Session["FullName"]?.ToString() ?? "";

                if (!string.IsNullOrEmpty(hfEditingMemberId.Value))
                {
                    // ── Edit existing Student / Teacher ──────────────────────
                    int memberId = int.Parse(hfEditingMemberId.Value);

                    DataTable userDt = DatabaseHelper.ExecuteQuery(
                        "SELECT UserID FROM tblMembers WHERE MemberID = @MemberID",
                        new SqlParameter[] { new SqlParameter("@MemberID", memberId) });

                    if (userDt.Rows.Count == 0) throw new Exception("Member not found.");
                    string userId = userDt.Rows[0]["UserID"].ToString();

                    DatabaseHelper.ExecuteNonQuery(
                        "UPDATE tblUsers SET Email = @Email WHERE UserID = @UserID",
                        new SqlParameter[]
                        {
                            new SqlParameter("@Email",  txtEmail.Text),
                            new SqlParameter("@UserID", userId)
                        });

                    if (!string.IsNullOrEmpty(txtPassword.Text))
                        DatabaseHelper.ExecuteNonQuery(
                            "UPDATE tblUsers SET PasswordHash = @PasswordHash WHERE UserID = @UserID",
                            new SqlParameter[]
                            {
                                new SqlParameter("@PasswordHash", DatabaseHelper.HashPassword(txtPassword.Text)),
                                new SqlParameter("@UserID",       userId)
                            });

                    if (selectedRole == "Teacher")
                        DatabaseHelper.ExecuteNonQuery(
                            "UPDATE tblMembers SET FullName=@FullName, MemberType='Teacher', Course=NULL, YearLevel=NULL WHERE MemberID=@MemberID",
                            new SqlParameter[]
                            {
                                new SqlParameter("@FullName", txtFullName.Text),
                                new SqlParameter("@MemberID", memberId)
                            });
                    else
                        DatabaseHelper.ExecuteNonQuery(
                            "UPDATE tblMembers SET FullName=@FullName, MemberType='Student', Course=@Course, YearLevel=@YearLevel WHERE MemberID=@MemberID",
                            new SqlParameter[]
                            {
                                new SqlParameter("@FullName",  txtFullName.Text),
                                new SqlParameter("@Course",    txtCourse.Text),
                                new SqlParameter("@YearLevel", ParseYearLevel(ddlYearLevel.SelectedValue)),
                                new SqlParameter("@MemberID",  memberId)
                            });

                    DatabaseHelper.WriteAuditLog(adminId, adminName, "EDIT_MEMBER", "tblMembers", memberId.ToString());
                }
                else
                {
                    // ── Add new Student / Teacher ────────────────────────────
                    string newUserId = txtUserId.Text.Trim();

                    DatabaseHelper.ExecuteNonQuery(
                        "INSERT INTO tblUsers (UserID, PasswordHash, Role, Email, IsActive) VALUES (@UserID, @PasswordHash, 'Member', @Email, 1)",
                        new SqlParameter[]
                        {
                            new SqlParameter("@UserID",       newUserId),
                            new SqlParameter("@PasswordHash", DatabaseHelper.HashPassword(txtPassword.Text)),
                            new SqlParameter("@Email",        txtEmail.Text)
                        });

                    if (selectedRole == "Teacher")
                        DatabaseHelper.ExecuteNonQuery(
                            "INSERT INTO tblMembers (UserID, FullName, MemberType) VALUES (@UserID, @FullName, 'Teacher')",
                            new SqlParameter[]
                            {
                                new SqlParameter("@UserID",   newUserId),
                                new SqlParameter("@FullName", txtFullName.Text)
                            });
                    else
                        DatabaseHelper.ExecuteNonQuery(
                            "INSERT INTO tblMembers (UserID, FullName, MemberType, Course, YearLevel) VALUES (@UserID, @FullName, 'Student', @Course, @YearLevel)",
                            new SqlParameter[]
                            {
                                new SqlParameter("@UserID",    newUserId),
                                new SqlParameter("@FullName",  txtFullName.Text),
                                new SqlParameter("@Course",    txtCourse.Text),
                                new SqlParameter("@YearLevel", ParseYearLevel(ddlYearLevel.SelectedValue))
                            });

                    DatabaseHelper.WriteAuditLog(adminId, adminName, "ADD_MEMBER", "tblMembers", newUserId);
                }

                ClearMemberForm();
                hfEditingMemberId.Value = "";
                LoadMembers();

                ScriptManager.RegisterStartupScript(this, GetType(), "closeModal",
                    "var m = bootstrap.Modal.getInstance(document.getElementById('memberModal')); if(m) m.hide();", true);
                ScriptManager.RegisterStartupScript(this, GetType(), "success",
                    "alert('Member saved successfully.');", true);
            }
            catch (Exception ex)
            {
                ScriptManager.RegisterStartupScript(this, GetType(), "error",
                    $"alert('Error saving member: {ex.Message}');", true);
            }
        }

        private void ClearMemberForm()
        {
            txtUserId.Text = ""; txtFullName.Text = ""; txtEmail.Text = "";
            txtCourse.Text = ""; ddlYearLevel.SelectedIndex = 0; txtPassword.Text = "";
            hfEditingMemberId.Value = "";
        }

        protected void gvMembers_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvMembers.PageIndex = e.NewPageIndex;
            LoadMembers();
        }

        protected void btnSearchMember_Click(object sender, EventArgs e)
        {
            SearchTerm = txtSearchMember.Text.Trim();
            gvMembers.PageIndex = 0;
            LoadMembers();
        }

        protected void ddlMembershipType_SelectedIndexChanged(object sender, EventArgs e) { gvMembers.PageIndex = 0; LoadMembers(); }
        protected void ddlStatus_SelectedIndexChanged(object sender, EventArgs e) { gvMembers.PageIndex = 0; LoadMembers(); }

        protected void gvMembers_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            try
            {
                int rowIndex = Convert.ToInt32(e.CommandArgument.ToString());
                string memberId = gvMembers.DataKeys[rowIndex]["MemberID"].ToString();
                string role = gvMembers.DataKeys[rowIndex]["Role"].ToString();
                string adminId = Session["UserID"]?.ToString() ?? "";
                string adminName = Session["FullName"]?.ToString() ?? "";

                if (e.CommandName == "ToggleStatus")
                {
                    DataTable memberDt = DatabaseHelper.ExecuteQuery(
                        "SELECT MemberID FROM tblMembers WHERE UserID = @UserID",
                        new SqlParameter[] { new SqlParameter("@UserID", memberId) });

                    if (memberDt.Rows.Count > 0)
                    {
                        string actualMemberId = memberDt.Rows[0]["MemberID"].ToString();
                        DataTable userDt = DatabaseHelper.ExecuteQuery(@"
                            SELECT u.UserID, u.IsActive
                            FROM tblUsers u
                            INNER JOIN tblMembers m ON u.UserID = m.UserID
                            WHERE m.MemberID = @MemberID",
                            new SqlParameter[] { new SqlParameter("@MemberID", actualMemberId) });

                        if (userDt.Rows.Count > 0)
                        {
                            string userId = userDt.Rows[0]["UserID"].ToString();
                            int newActive = Convert.ToInt32(userDt.Rows[0]["IsActive"]) == 1 ? 0 : 1;
                            DatabaseHelper.ExecuteNonQuery(
                                "UPDATE tblUsers SET IsActive = @IsActive WHERE UserID = @UserID",
                                new SqlParameter[]
                                {
                                    new SqlParameter("@IsActive", newActive),
                                    new SqlParameter("@UserID",   userId)
                                });
                            DatabaseHelper.WriteAuditLog(adminId, adminName,
                                newActive == 1 ? "ACTIVATE_MEMBER" : "DEACTIVATE_MEMBER", "tblUsers", userId);
                        }
                    }
                    LoadMembers();
                    return;
                }

                if (e.CommandName == "DeleteMember")
                {
                    DataTable memberDt = DatabaseHelper.ExecuteQuery(
                        "SELECT MemberID FROM tblMembers WHERE UserID = @UserID",
                        new SqlParameter[] { new SqlParameter("@UserID", memberId) });

                    if (memberDt.Rows.Count > 0)
                    {
                        string actualMemberId = memberDt.Rows[0]["MemberID"].ToString();

                        DataTable checkDt = DatabaseHelper.ExecuteQuery(
                            "SELECT COUNT(*) FROM tblTransactions WHERE MemberID = @MemberID AND Status = 'Active'",
                            new SqlParameter[] { new SqlParameter("@MemberID", actualMemberId) });

                        if (Convert.ToInt32(checkDt.Rows[0][0]) > 0)
                        {
                            ScriptManager.RegisterStartupScript(this, GetType(), "alert",
                                "alert('Cannot delete a member with active borrow transactions.');", true);
                            return;
                        }

                        DataTable userDt = DatabaseHelper.ExecuteQuery(
                            "SELECT UserID FROM tblMembers WHERE MemberID = @MemberID",
                            new SqlParameter[] { new SqlParameter("@MemberID", actualMemberId) });

                        if (userDt.Rows.Count > 0)
                        {
                            string userId = userDt.Rows[0]["UserID"].ToString();
                            DatabaseHelper.WriteAuditLog(adminId, adminName, "DELETE_MEMBER", "tblUsers", userId);
                            DatabaseHelper.ExecuteNonQuery("DELETE FROM tblUsers WHERE UserID = @UserID",
                                new SqlParameter[] { new SqlParameter("@UserID", userId) });
                        }
                    }

                    LoadMembers();
                    ScriptManager.RegisterStartupScript(this, GetType(), "success",
                        "alert('Member deleted successfully.');", true);
                    return;
                }

                if (e.CommandName == "EditMember")
                {
                    DataTable memberDt = DatabaseHelper.ExecuteQuery(
                        "SELECT MemberID FROM tblMembers WHERE UserID = @UserID",
                        new SqlParameter[] { new SqlParameter("@UserID", memberId) });

                    if (memberDt.Rows.Count > 0)
                    {
                        string actualMemberId = memberDt.Rows[0]["MemberID"].ToString();
                        DataTable dt = DatabaseHelper.ExecuteQuery(@"
                            SELECT m.MemberID, m.FullName, m.MemberType, m.Course, m.YearLevel,
                                   u.UserID, u.Email, u.IsActive
                            FROM tblMembers m
                            INNER JOIN tblUsers u ON m.UserID = u.UserID
                            WHERE m.MemberID = @MemberID",
                            new SqlParameter[] { new SqlParameter("@MemberID", actualMemberId) });

                        if (dt.Rows.Count > 0)
                        {
                            DataRow row = dt.Rows[0];
                            string memberType = row["MemberType"].ToString();

                            txtUserId.Text = row["UserID"].ToString();
                            txtFullName.Text = row["FullName"].ToString();
                            txtEmail.Text = row["Email"].ToString();
                            txtPassword.Text = "";
                            hfEditingMemberId.Value = row["MemberID"].ToString();
                            lblRegisterTitle.Text = "Edit Member";

                            if (memberType == "Teacher")
                            {
                                txtCourse.Text = "";
                                ddlYearLevel.SelectedIndex = 0;
                                ScriptManager.RegisterStartupScript(this, GetType(), "setType",
                                    "selectMemberType('Teacher');", true);
                            }
                            else
                            {
                                txtCourse.Text = row["Course"]?.ToString() ?? "";
                                ddlYearLevel.SelectedValue = YearLevelToText(
                                    row["YearLevel"] != DBNull.Value ? Convert.ToInt32(row["YearLevel"]) : 1);
                                ScriptManager.RegisterStartupScript(this, GetType(), "setType",
                                    "selectMemberType('Student');", true);
                            }

                            ScriptManager.RegisterStartupScript(this, GetType(), "openModal",
                                "new bootstrap.Modal(document.getElementById('memberModal')).show();", true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ScriptManager.RegisterStartupScript(this, GetType(), "error",
                    $"alert('Error: {ex.Message}');", true);
            }
        }

        protected string GetStatusBadgeClass(object statusObj) =>
            statusObj?.ToString() == "Active" ? "status-active" : "status-inactive";

        protected string GetMemberStatus(object statusObj) =>
            statusObj?.ToString() == "Active" ? "Active" : "Inactive";

        private int ParseYearLevel(string text)
        {
            switch (text)
            {
                case "1st Year": return 1;
                case "2nd Year": return 2;
                case "3rd Year": return 3;
                case "4th Year": return 4;
                default: return int.TryParse(text?.Substring(0, 1), out int v) ? v : 1;
            }
        }

        private string YearLevelToText(int y)
        {
            switch (y) { case 1: return "1st Year"; case 2: return "2nd Year"; case 3: return "3rd Year"; case 4: return "4th Year"; default: return "1st Year"; }
        }
    }
}