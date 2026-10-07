# 📄 SCHOOL MANAGEMENT SYSTEM (SMS)
## Comprehensive Technical Overview & Feature Specification

### 1. Executive Summary

The School Management System (SMS) is a web-based school portal for managing student and staff accounts, academic records, attendance, events, and school communications. Its public pages provide school information and event listings; authenticated users receive role-specific dashboards and tools.

The system is intended for school administrators, teachers, students, and guardians. It centralizes class and subject administration, lets teachers record attendance and marks, and gives students access to published academic results. A public application workflow lets prospective students and teachers submit registration requests for administrative review.

The application is split between a React single-page application (SPA) and an ASP.NET Core REST API. The API uses Entity Framework Core (EF Core) with Microsoft SQL Server for persistence and ASP.NET Core Identity/JWT for identity and access control.

### 2. Technical Stack & Architecture

| Layer | Technology / Tools |
| :--- | :--- |
| **Frontend UI** | React 18, Vite, Tailwind CSS 4, Lucide React icons, Framer Motion |
| **Backend API** | ASP.NET Core Web API, C#, Entity Framework Core |
| **Database** | Microsoft SQL Server |
| **Authentication** | ASP.NET Core Identity, JWT access tokens, refresh tokens, Role-Based Access Control (RBAC) |
| **Reports & Email** | QuestPDF, SendGrid |

The frontend uses React Router for public and protected pages, a shared authentication context, and API clients for communicating with the backend. The backend organizes HTTP endpoints into controllers, uses DTOs for request/response contracts, and separates selected business operations into services. EF Core maps application entities to SQL Server tables and views.

The database schema is maintained through ordered SQL scripts in `database/`. Although the project references EF Core design tooling, its project documentation and configuration designate the SQL scripts—not EF Core migrations—as the schema deployment mechanism.

### 3. Core Feature Catalog

#### 3.1 Student Self-Registration & Admin Approval Workflow

- The login page exposes the registration form for student and teacher applicants. Student applications include the requested grade and section; applicants provide a contact email address.
- Submitting an application creates a **Pending** registration request. It does not sign the applicant in or immediately create an active account.
- The admin review queue is part of **Admin Accounts** (`/admin/accounts`), rather than a separate `PendingApprovals.jsx` page. Administrators can view pending, approved, and rejected requests and approve or reject pending applications.
- Approval provisions the account and, for a student request, the student record. The generated student identifier follows `sms-{year}-{four-digit sequence}` (for example, `sms-2026-0001`). The sequence is generated within a serialized database transaction to protect concurrent provisioning.
- The default generated student sign-in address is `{StudentId}@education.edu` (for example, `sms-2026-0001@education.edu`). The domain is configurable through provisioning settings.
- The applicant's supplied address remains the contact address for delivery of the issued sign-in details. Generated accounts may receive a temporary password; Identity stores password hashes rather than recoverable plaintext passwords.
- A duplicate pending application for the same contact email is rejected. The request history retains review status and reviewer information.

#### 3.2 Multi-Grade Academic Structure (KG–12)

- The SQL grade catalog covers **Nursery, KG, LKG, UKG, and Grade 1 through Grade 12**.
- Sections are represented as shared school sections **Section A through Section F**. The later SQL update adds Sections D, E, and F while preserving existing names and codes.
- Grade levels and sections are used by enrollment, student records, class/roster views, and teacher assignment workflows. Administrators can manage active grade levels; teacher-facing rosters are scoped to teaching assignments, while students see their own assigned class and results.
- The structure is catalog-backed: the database stores grade levels and sections as separate records rather than hard-coding a distinct A–F section set for every grade.

#### 3.3 Role-Based Access & Feature Breakdown

| Role | Implemented capabilities |
| :--- | :--- |
| **Admin** | Manage student and staff accounts; review registration requests; manage grade levels, subjects, teacher assignments, attendance, and assessments; view administrative analytics and feedback; create and manage school events; manage system settings. |
| **Teacher** | View assigned students and class rosters; record attendance; enter marks for assigned classes; view teacher analytics; use lesson/material workflows; participate in event discussions. |
| **Student** | View personal academic results and published marks; download available results/report PDFs; submit feedback; view events and participate in event discussions. |
| **Guardian / Parent** | Administrators create guardian accounts and link them to one or more students. The parent dashboard shows linked students, attendance, published results, and a downloadable report card. Backend access is scoped to each guardian's own student links. |

Frontend protected routes enforce the Admin, Teacher, Student, and Guardian page boundaries. Public registration is limited to student and teacher applicants; guardian account creation is administrator-only. API authorization remains authoritative for protected operations and student/guardian-specific data.

#### 3.4 Interactive Calendar & Event Discussions

- The public **Events** page presents events in a searchable, filterable card list. It supports category and event-status filters; it is not implemented as a month-grid calendar view.
- Administrators can create, edit, change status, and remove events. Event data supports dates/times, location, organizer, audience targeting, and grade/section scopes.
- Event discussion threads are persisted in the database. Students, teachers, and administrators can view and post comments; the API also provides administrator reply functionality.
- Event registration and cancellation are represented by backend operations. The frontend shows event information, categories, statuses, and discussions.
- Signed-in dashboards also include a shared events widget.

#### 3.5 Academic Operations

- **Attendance:** Admin and teacher interfaces support attendance review and recording, including bulk entry. The API stores attendance by student and date and enforces a unique student/date record.
- **Assessments and gradebook:** Administrators can manage assessments and grading configuration; teachers enter student marks. Student-facing result views are limited to the signed-in student's own published results.
- **Report cards:** The backend calculates weighted academic results and provides downloadable PDF report cards. Student access is restricted to their own report, and Guardian access is checked against the linked-student relationship.
- **Subjects and teaching assignments:** Administrators manage subject catalogs and assign teachers to subjects/classes; teacher rosters and teaching views are based on those assignments.
- **Parent/guardian management:** Administrators register guardian accounts, receive a one-time temporary password, and link guardians to students with a recorded relationship. Duplicate links are blocked by the unique guardian/student pair constraint.
- **Parent dashboard:** Guardian accounts select among linked students and can view each child's profile, attendance history, published academic results, and PDF report card. Every student-specific API request verifies the authenticated guardian's user ID against the student-link table; unlinked student IDs receive `403 Forbidden`.
- **Lessons and learning materials:** The API provides lesson CRUD and lesson-file upload/download operations for authorized staff.
- **Timetable scheduling:** A weekly timetable or class-period scheduling feature was not identified. Lesson records and event schedules should not be treated as a timetable module.

### 4. Security & Data Integrity

- **Authentication and roles:** ASP.NET Core Identity manages users and roles. JWT bearer authentication validates issuer, audience, signing key, and token lifetime. Refresh-token and logout endpoints support continued sessions and token revocation.
- **Password handling:** Accounts are managed through Identity's password hashing. The configured password policy requires at least eight characters, including uppercase, lowercase, numeric, and non-alphanumeric characters. Repeated failed sign-ins trigger account lockout.
- **Authorization:** Backend controllers apply role-based authorization. Sensitive student results and guardian report access also verify that the requesting account is associated with the requested student.
- **Registration and provisioning:** Student account creation and approval state changes use database transactions; student-number generation runs under serializable transaction isolation. Duplicate application protection is backed by database uniqueness checks.
- **Database constraints:** EF Core model configuration and SQL scripts define relationships, indexes, unique constraints, defaults, and data checks for core school entities.
- **Schema deployment and seed data:** Database structure changes are supplied as numbered T-SQL scripts in `database/`. `DbSeeder.cs` seeds Identity roles and configured accounts; development demo accounts are environment-gated. The implementation uses `DbSeeder`, not a class named `DbInitializer`.
- **Configuration:** Connection strings, JWT signing keys, email credentials, and seed passwords are environment-specific configuration and should be supplied through secure deployment configuration rather than published documentation.

### 5. Implementation Notes

- The requested feature names do not exactly match all current source filenames: registration is implemented by `StudentRegistrationForm.jsx` on the login page, and request review is integrated into `AdminAccounts.jsx`.
- The Guardian role has its own protected React dashboard and admin management page.
- Events have calendar dates and category filters, but the user interface is a list of event cards rather than an interactive calendar grid.
- Lesson/material management is present; a weekly timetable scheduler was not found.
- The database source of truth for schema updates is the numbered SQL script series, not an EF Core migration history.

### 6. Exporting This File to PDF in VS Code or Cursor

1. Open `feature of projects.md` in VS Code or Cursor.
2. Install a Markdown-to-PDF extension, such as **Markdown PDF**, if one is not already available.
3. Open the Command Palette (`Ctrl+Shift+P`) and run the extension's **Markdown PDF: Export (pdf)** command.
4. Choose an output location when prompted. The generated PDF can then be reviewed and printed or shared.
