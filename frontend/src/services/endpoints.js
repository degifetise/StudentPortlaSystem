import api from './api';

/**
 * One place that knows the API's URL shapes, so a route change never has to be
 * chased through the pages. Every function resolves to the response body.
 */
const unwrap = (promise) => promise.then(({ data }) => data);

function toMultipartFormData(payload) {
  const formData = new FormData();
  Object.entries(payload).forEach(([key, value]) => {
    if (value !== null && value !== undefined && value !== '') {
      formData.append(key, value instanceof File ? value : String(value));
    }
  });
  return formData;
}

async function downloadPdf(path, fallbackFilename) {
  let response;
  try {
    response = await api.get(path, { responseType: 'blob' });
  } catch (error) {
    console.error('PDF download request failed', {
      url: api.getUri({ url: path }),
      status: error?.response?.status ?? null,
      message: error?.message ?? 'Unknown request error',
    });
    throw error;
  }

  const { data, headers } = response;
  const disposition = headers['content-disposition'] ?? '';
  const encodedFilename = disposition.match(/filename\*=UTF-8''([^;]+)/i)?.[1];
  const plainFilename = disposition.match(/filename="?([^";]+)"?/i)?.[1];
  let filename = encodedFilename ?? plainFilename ?? fallbackFilename;

  try {
    filename = decodeURIComponent(filename);
  } catch {
    filename = plainFilename ?? fallbackFilename;
  }

  const objectUrl = URL.createObjectURL(new Blob([data], { type: 'application/pdf' }));
  const link = document.createElement('a');
  link.href = objectUrl;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
}

export const reportPdfApi = {
  studentResults: () => downloadPdf('/api/assessments/my-results/pdf', 'academic-results.pdf'),
  parentStudentReportCard: (studentId) =>
    downloadPdf(`/api/parent/student/${studentId}/report-card-pdf`, `report-card-${studentId}.pdf`),
  studentsRoster: (params = {}) =>
    downloadPdf(`/api/admin/reports/students/pdf${params.gradeLevelId || params.sectionId
      ? `?${new URLSearchParams(Object.entries(params).filter(([, value]) => value)).toString()}`
      : ''}`, 'students-roster.pdf'),
  teachersRoster: () => downloadPdf('/api/admin/reports/teachers/pdf', 'faculty-directory.pdf'),
  teacherSectionRoster: (sectionId) =>
    downloadPdf(`/api/teacher/reports/section/${sectionId}/pdf`, `section-${sectionId}-roster.pdf`),
};

export const schoolApi = {
  /** Anonymous: used by the login screen and the dashboard header. */
  getInfo: () => unwrap(api.get('/api/system-settings/school-info')),
};

/** The anonymous surface behind the Home, About and Explore events pages. */
export const publicApi = {
  overview: () => unwrap(api.get('/api/public/overview')),
  events: (take) => unwrap(api.get('/api/public/events', { params: take ? { take } : {} })),
};

export const eventApi = {
  list: (params = {}) => unwrap(api.get('/api/v1/events', { params })),
  get: (id) => unwrap(api.get(`/api/v1/events/${id}`)),
  create: (payload) => unwrap(api.post('/api/v1/events', payload)),
  update: (id, payload) => unwrap(api.put(`/api/v1/events/${id}`, payload)),
  updateStatus: (id, status) => unwrap(api.patch(`/api/v1/events/${id}/status`, { status })),
  remove: (id) => unwrap(api.delete(`/api/v1/events/${id}`)),
  register: (id, studentId) => unwrap(api.post(`/api/v1/events/${id}/register`, { studentId })),
  cancelRegistration: (id, studentId) => unwrap(api.post(`/api/v1/events/${id}/cancel-registration`, { studentId })),
  registrations: (id) => unwrap(api.get(`/api/v1/events/${id}/registrations`)),
  comments: (id) => unwrap(api.get(`/api/events/${id}/comments`)),
  addComment: (id, commentText, parentCommentId = null) =>
    unwrap(api.post(`/api/events/${id}/comments`, { commentText, parentCommentId })),
  replyToComment: (id, parentCommentId, commentText) =>
    unwrap(api.post(`/api/events/${id}/comments/reply`, { parentCommentId, commentText })),
  dashboardStats: (count = 5) => unwrap(api.get('/api/v1/events/dashboard/stats', { params: { count } })),
};

export const healthApi = {
  dbCheck: () => unwrap(api.get('/api/health/db-check')),
};

export const analyticsApi = {
  adminOverview: () => unwrap(api.get('/api/admin/analytics/overview')),
  teacherPerformance: () => unwrap(api.get('/api/teacher/analytics/performance')),
};

export const feedbackApi = {
  mine: () => unwrap(api.get('/api/feedback')),
  submit: (payload) => unwrap(api.post('/api/feedback', payload)),
  list: () => unwrap(api.get('/api/admin/feedback')),
  updateStatus: (id, status) => unwrap(api.put(`/api/admin/feedback/${id}/status`, { status })),
};

export const bulkAdminApi = {
  assignTeacher: (payload) => unwrap(api.post('/api/allocations/assign-bulk', payload)),
  enrollStudents: (payload) => unwrap(api.post('/api/students/bulk-enroll', payload)),
};

export const authApi = {
  login: (email, password) => unwrap(api.post('/api/auth/login', { email, password })),
  me: () => unwrap(api.get('/api/auth/me')),
  logout: (refreshToken) => unwrap(api.post('/api/auth/logout', { refreshToken })),
  /**
   * Anonymous application for a place. Resolves to a receipt, not a session: no account exists
   * until an administrator approves it and issues credentials.
   */
  registerStudent: (payload) =>
    unwrap(api.post('/api/auth/register-student-with-photo', toMultipartFormData(payload))),
};

export const smartIdApi = {
  cardDetails: (userId) => unwrap(api.get(`/api/smart-id/card-details/${encodeURIComponent(userId)}`)),
  paymentStatus: (userId) => unwrap(api.get(`/api/smart-id/payment-status/${encodeURIComponent(userId)}`)),
  adminLookup: (identifier) =>
    unwrap(api.get(`/api/smart-id/admin/users/${encodeURIComponent(identifier)}`)),
  adminCards: (params = {}) => unwrap(api.get('/api/smart-id/admin/cards', { params })),
  generateForUser: (userId) =>
    unwrap(api.post(`/api/smart-id/admin/users/${encodeURIComponent(userId)}/generate`)),
  updateAdminCard: (userId, payload) =>
    unwrap(api.put(`/api/smart-id/admin/cards/${encodeURIComponent(userId)}`, payload)),
  deleteAdminCard: (userId) =>
    unwrap(api.delete(`/api/smart-id/admin/cards/${encodeURIComponent(userId)}`)),
  batchCards: (userIds, filters = {}) =>
    unwrap(api.post('/api/smart-id/batch-cards', { userIds }, { params: filters })),
  generateCards: (userIds, filters = {}) =>
    unwrap(api.post('/api/smart-id/generate-cards', { userIds }, { params: filters })),
  generateQrToken: (userId) =>
    unwrap(api.get(`/api/smart-id/generate-qr-token/${encodeURIComponent(userId)}`)),
  verifyScan: (code, scanLocation) =>
    unwrap(api.post('/api/smart-id/verify-scan', { code, scanLocation })),
  updateCardStatus: (userId, status) =>
    unwrap(api.put(`/api/smart-id/cards/${encodeURIComponent(userId)}/status`, { status })),
  uploadPhoto: (userId, file) => {
    const body = new FormData();
    body.append('file', file);
    return unwrap(api.post(`/api/users/${encodeURIComponent(userId)}/upload-photo`, body));
  },
  uploadSignature: (userId, file) => {
    const body = new FormData();
    body.append('file', file);
    return unwrap(api.post(`/api/smartcard/upload-signature/${encodeURIComponent(userId)}`, body));
  },
};

/** The signed-in user's own account, whatever their role. */
export const accountApi = {
  changePassword: (currentPassword, newPassword) =>
    unwrap(api.post('/api/account/change-password', { currentPassword, newPassword })),
};

/** The administrator's student registration queue. */
export const registrationApi = {
  /** Applications by review state: Pending, Approved or Rejected. */
  list: (status = 'Pending') =>
    unwrap(api.get('/api/admin/registration-requests', { params: { status } })),
  /** Resolves to the issued credentials, including the one-time temporary password. */
  approve: (id, note) =>
    unwrap(api.post(`/api/admin/registration-requests/${id}/approve`, { note: note ?? null })),
  reject: (id, note) =>
    unwrap(api.post(`/api/admin/registration-requests/${id}/reject`, { note: note ?? null })),
};

export const guardianApi = {
  list: () => unwrap(api.get('/api/admin/guardians')),
  get: (id) => unwrap(api.get(`/api/admin/guardians/${id}`)),
  registerParent: (payload) => unwrap(api.post('/api/admin/register-parent', payload)),
  linkStudent: (payload) => unwrap(api.post('/api/admin/guardians/link-student', payload)),
  updateLink: (payload) => unwrap(api.put('/api/admin/guardians/update-link', payload)),
  unlinkStudent: (guardianId, studentId) => unwrap(api.delete(`/api/admin/guardians/${guardianId}/students/${studentId}`)),
  myStudents: () => unwrap(api.get('/api/parent/my-students')),
  studentAttendance: (studentId) => unwrap(api.get(`/api/parent/student/${studentId}/attendance`)),
  studentResults: (studentId) => unwrap(api.get(`/api/parent/student/${studentId}/results`)),
};

export const gradeLevelApi = {
  list: () => unwrap(api.get('/api/grade-levels')),
};

export const sectionApi = {
  list: () => unwrap(api.get('/api/sections')),
};

export const subjectApi = {
  list: (params = {}) => unwrap(api.get('/api/subjects', { params })),
  mine: () => unwrap(api.get('/api/subjects/mine')),
  create: (payload) => unwrap(api.post('/api/subjects', payload)),
  update: (id, payload) => unwrap(api.put(`/api/subjects/${id}`, payload)),
  remove: (id) => unwrap(api.delete(`/api/subjects/${id}`)),
};

export const studentApi = {
  list: (params = {}) => unwrap(api.get('/api/students', { params })),
  get: (id) => unwrap(api.get(`/api/students/${id}`)),
  create: (payload) => unwrap(api.post('/api/students/with-photo', toMultipartFormData(payload))),
  update: (id, payload) => unwrap(api.put(`/api/students/${id}`, payload)),
  setStatus: (id, isActive) => unwrap(api.put(`/api/students/${id}/status`, { isActive })),
  resetPassword: (id) => unwrap(api.post(`/api/students/${id}/reset-password`)),
  summary: () => unwrap(api.get('/api/students/summary')),

  /**
   * The signed-in student's own results: subjects with component scores, weighted totals,
   * the grade summary and the weighting behind them, in one call.
   */
  myResults: () => unwrap(api.get('/api/students/my-results')),
  /** Published Grade 11-12 subject history with term and cumulative GPA. */
  myTranscript: () => unwrap(api.get('/api/students/my-transcript')),
};

export const teacherApi = {
  list: (params = {}) => unwrap(api.get('/api/teachers', { params })),
  get: (id) => unwrap(api.get(`/api/teachers/${id}`)),
  create: (payload) => unwrap(api.post('/api/teachers/with-photo', toMultipartFormData(payload))),
  setStatus: (id, isActive) => unwrap(api.put(`/api/teachers/${id}/status`, { isActive })),
  resetPassword: (id) => unwrap(api.post(`/api/teachers/${id}/reset-password`)),
  assign: (teacherId, payload) => unwrap(api.post(`/api/teachers/${teacherId}/assignments`, payload)),
  removeAssignment: (teacherId, assignmentId) =>
    unwrap(api.delete(`/api/teachers/${teacherId}/assignments/${assignmentId}`)),
  reassignAssignment: (assignmentId, teacherId) =>
    unwrap(api.put(`/api/admin/teacher-assignments/${assignmentId}`, { teacherId })),
  /** The signed-in teacher's own subject/section assignments. */
  myClasses: () => unwrap(api.get('/api/teachers/me/classes')),
  /** Class list for one of those assignments, with each student's weighted standing. */
  classRoster: async (assignmentId) => {
    try {
      return await unwrap(api.get(`/api/teachers/me/classes/${assignmentId}/students`));
    } catch (error) {
      error.friendlyMessage =
        error.friendlyMessage ?? 'We could not load this class list. Please refresh and try again.';
      throw error;
    }
  },
};

export const staffApi = {
  create: (payload) => unwrap(api.post('/api/staff/with-photo', toMultipartFormData(payload))),
};

export const attendanceApi = {
  list: (params = {}) => unwrap(api.get('/api/attendance', { params })),
  summary: (params = {}) => unwrap(api.get('/api/attendance/summary', { params })),
  mark: (payload) => unwrap(api.post('/api/attendance', payload)),
  bulkMark: (payload) => unwrap(api.post('/api/attendance/bulk', payload)),
};

export const assessmentApi = {
  list: (params = {}) => unwrap(api.get('/api/assessments', { params })),
  create: (payload) => unwrap(api.post('/api/assessments', payload)),
  update: (id, payload) => unwrap(api.put(`/api/assessments/${id}`, payload)),
  remove: (id) => unwrap(api.delete(`/api/assessments/${id}`)),
};

export const markApi = {
  /** Class list for one assessment, with any scores already entered. */
  gradebook: (assessmentId) => unwrap(api.get(`/api/marks/assessment/${assessmentId}`)),
  saveBulk: (payload) => unwrap(api.post('/api/marks/bulk', payload)),
  publish: (assessmentId, isPublished) =>
    unwrap(api.put(`/api/marks/assessment/${assessmentId}/publish`, { isPublished })),
  /** Student's own published marks. Their report card comes from studentApi.myResults. */
  mine: (params = {}) => unwrap(api.get('/api/marks/me', { params })),
  weights: () => unwrap(api.get('/api/marks/weights')),
};

export const announcementApi = {
  list: (params = {}) => unwrap(api.get('/api/announcements', { params })),
  create: (payload) => unwrap(api.post('/api/announcements', payload)),
  remove: (id) => unwrap(api.delete(`/api/announcements/${id}`)),
};

export const systemSettingsApi = {
  get: () => unwrap(api.get('/api/system-settings')),
  update: (payload) => unwrap(api.put('/api/system-settings', payload)),
};

export const notificationApi = {
  unreadCount: () => unwrap(api.get('/api/notifications/unread-count')),
  list: () => unwrap(api.get('/api/notifications')),
  markRead: (id) => unwrap(api.put(`/api/notifications/${id}/read`)),
  markAllRead: () => unwrap(api.put('/api/notifications/read-all')),
};
