import { lazy, Suspense } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { ROLES } from '../context/AuthContext';
import ProtectedRoute from './ProtectedRoute';
import PublicLayout from '../components/layout/PublicLayout';
import DashboardLayout from '../components/layout/DashboardLayout';
const HomePage = lazy(() => import('../pages/HomePage'));
const AboutPage = lazy(() => import('../pages/AboutPage'));
const EventsPage = lazy(() => import('../pages/EventsPage'));
const LoginPage = lazy(() => import('../pages/LoginPage'));
const SettingsPage = lazy(() => import('../pages/SettingsPage'));
const NotFoundPage = lazy(() => import('../pages/NotFoundPage'));
const AdminStudents = lazy(() => import('../pages/admin/AdminStudents'));
const AdminAttendance = lazy(() => import('../pages/admin/AdminAttendance'));
const AdminAssessments = lazy(() => import('../pages/admin/AdminAssessments'));
const AdminAccounts = lazy(() => import('../pages/admin/AdminAccounts'));
const TeacherStudents = lazy(() => import('../pages/teacher/TeacherStudents'));
const EnterMarks = lazy(() => import('../pages/teacher/EnterMarks'));
const MyResults = lazy(() => import('../pages/student/MyResults'));
const TeacherAssignmentsPage = lazy(() => import('../pages/admin/TeacherAssignmentsPage'));
const SubjectManagementPage = lazy(() => import('../pages/admin/SubjectManagementPage'));
const AttendancePage = lazy(() => import('../pages/teacher/AttendancePage'));
const AdminAnalytics = lazy(() => import('../pages/admin/AdminAnalytics'));
const TeacherAnalytics = lazy(() => import('../pages/teacher/TeacherAnalytics'));
const StudentFeedback = lazy(() => import('../pages/student/StudentFeedback'));
const AdminFeedback = lazy(() => import('../pages/admin/AdminFeedback'));
const AdminGuardians = lazy(() => import('../pages/admin/AdminGuardians'));
const ParentDashboard = lazy(() => import('../pages/parent/ParentDashboard'));
const SmartIDPrintPage = lazy(() => import('../pages/admin/SmartIDPrintPage'));
const SmartIDScannerPage = lazy(() => import('../pages/SmartIDScannerPage'));
const MySmartIDPage = lazy(() => import('../pages/MySmartIDPage'));

function SmartIDPageFallback() {
  return <p role="status" className="text-sm text-slate-500">Loading Smart ID tools…</p>;
}

function PageFallback() {
  return <p role="status" className="text-sm text-slate-500">Loading page…</p>;
}

export default function AppRoutes() {
  return (
    <Suspense fallback={<PageFallback />}>
      <Routes>
      <Route path="/login" element={<LoginPage />} />

      {/* Anyone, signed in or not. Home is a real landing page rather than a redirect, so a
          visitor can read about the school before being asked for a password. */}
      <Route element={<PublicLayout />}>
        <Route path="/" element={<HomePage />} />
        <Route path="/about" element={<AboutPage />} />
        <Route path="/events" element={<EventsPage />} />
      </Route>

      <Route element={<ProtectedRoute />}>
        <Route element={<DashboardLayout />}>
          {/* Every role: the page shows their own account, and the school-wide settings to an
              administrator, which is why the navigation bar needs only one Settings link. */}
          <Route path="/settings" element={<SettingsPage />} />
          <Route element={<ProtectedRoute allowedRoles={[ROLES.admin, ROLES.teacher, ROLES.staff, ROLES.student]} />}>
            <Route path="/my-smart-id" element={<Suspense fallback={<SmartIDPageFallback />}><MySmartIDPage /></Suspense>} />
          </Route>

          <Route element={<ProtectedRoute allowedRoles={[ROLES.admin]} />}>
            <Route path="/admin/smart-id/print" element={<Suspense fallback={<SmartIDPageFallback />}><SmartIDPrintPage /></Suspense>} />
            <Route path="/admin/scanner" element={<Suspense fallback={<SmartIDPageFallback />}><SmartIDScannerPage /></Suspense>} />
            <Route path="/admin/analytics" element={<AdminAnalytics />} />
            <Route path="/admin/students" element={<AdminStudents />} />
            <Route path="/admin/attendance" element={<AdminAttendance />} />
            <Route path="/admin/assessments" element={<AdminAssessments />} />
            <Route path="/admin/accounts" element={<AdminAccounts />} />
            <Route path="/admin/subjects" element={<SubjectManagementPage />} />
            <Route path="/admin/assignments" element={<TeacherAssignmentsPage />} />
            <Route path="/admin/feedback" element={<AdminFeedback />} />
            <Route path="/admin/guardians" element={<AdminGuardians />} />
            <Route path="/admin/parents" element={<AdminGuardians />} />
            {/* Kept alive so older links and bookmarks still land somewhere useful. */}
            <Route path="/admin" element={<Navigate to="/admin/students" replace />} />
            <Route path="/admin/settings" element={<Navigate to="/settings" replace />} />
          </Route>

          <Route element={<ProtectedRoute allowedRoles={[ROLES.teacher, ROLES.staff]} />}>
            <Route path="/staff/scanner" element={<Suspense fallback={<SmartIDPageFallback />}><SmartIDScannerPage /></Suspense>} />
          </Route>

          {/* Teacher only: pages are driven by /api/teachers/me/..., which resolves the
              caller's own teaching load and is not available to staff or admin accounts. */}
          <Route element={<ProtectedRoute allowedRoles={[ROLES.teacher]} />}>
            <Route path="/teacher/analytics" element={<TeacherAnalytics />} />
            <Route path="/teacher/students" element={<TeacherStudents />} />
            <Route path="/teacher/attendance" element={<AttendancePage />} />
            {/* Absent from the navigation bar by design: reached from a class list, which
                passes the class along in the query string. */}
            <Route path="/teacher/marks" element={<EnterMarks />} />
          </Route>

          <Route element={<ProtectedRoute allowedRoles={[ROLES.student]} />}>
            <Route path="/student/results" element={<MyResults />} />
            <Route path="/student/feedback" element={<StudentFeedback />} />
          </Route>

          <Route element={<ProtectedRoute allowedRoles={[ROLES.guardian]} />}>
            <Route path="/parent/dashboard" element={<ParentDashboard />} />
          </Route>
        </Route>
      </Route>

      {/* Outside the protected tree: an unknown URL should say so, not demand a sign-in. */}
      <Route element={<PublicLayout />}>
        <Route path="*" element={<NotFoundPage />} />
      </Route>
      </Routes>
    </Suspense>
  );
}
