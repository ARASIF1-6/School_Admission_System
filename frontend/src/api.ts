export interface ClassItem {
  id: string;
  name: string;
  totalSeats: number;
  heldSeats: number;
  confirmedSeats: number;
  availableSeats: number;
}

export interface HoldResponse {
  holdId: string;
  classId: string;
  studentId: string;
  status: 'HELD' | 'CONFIRMED' | 'EXPIRED' | 'CANCELLED';
  expiresAt: string;
}

export interface MyHoldItem {
  holdId: string;
  classId: string;
  className: string;
  status: 'HELD' | 'CONFIRMED' | 'EXPIRED' | 'CANCELLED';
  expiresAt: string;
}

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:8080';

export class ApiError extends Error {
  statusCode: number;
  constructor(message: string, statusCode: number) {
    super(message);
    this.statusCode = statusCode;
    this.name = 'ApiError';
  }
}

async function request<T>(endpoint: string, options: RequestInit = {}, studentId?: string): Promise<T> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string> || {}),
  };

  if (studentId) {
    headers['X-Student-Id'] = studentId;
  }

  let response: Response;
  try {
    response = await fetch(`${API_BASE}${endpoint}`, {
      ...options,
      headers,
    });
  } catch (err) {
    throw new ApiError('সার্ভারের সাথে সংযোগ করা যাচ্ছে না। অনুগ্রহ করে নিশ্চিত করুন যে ব্যাকএন্ড চলছে (Cannot connect to backend server at ' + API_BASE + ')', 0);
  }

  if (response.status === 204) {
    return {} as T;
  }

  let data: any;
  try {
    data = await response.json();
  } catch {
    data = null;
  }

  if (!response.ok) {
    const errorMsg = data?.message || data?.title || `Request failed with status ${response.status}`;
    throw new ApiError(errorMsg, response.status);
  }

  return data as T;
}

export const api = {
  // #1: POST /classes
  createClass: (name: string, totalSeats: number) =>
    request<{ id: string; name: string; totalSeats: number }>('/classes', {
      method: 'POST',
      body: JSON.stringify({ name, totalSeats }),
    }),

  // #2: GET /classes
  getClasses: () => request<ClassItem[]>('/classes'),

  // #3: GET /classes/{classId}
  getClassById: (classId: string) => request<ClassItem>(`/classes/${classId}`),

  // #4: POST /classes/{classId}/holds
  createHold: (classId: string, studentId: string) =>
    request<HoldResponse>(`/classes/${classId}/holds`, { method: 'POST' }, studentId),

  // #5: GET /holds/{holdId}
  getHoldById: (holdId: string) => request<HoldResponse>(`/holds/${holdId}`),

  // #6: POST /holds/{holdId}/confirm
  confirmHold: (holdId: string, studentId: string) =>
    request<{ holdId: string; status: string }>(`/holds/${holdId}/confirm`, { method: 'POST' }, studentId),

  // #7: DELETE /holds/{holdId}
  cancelHold: (holdId: string, studentId: string) =>
    request<void>(`/holds/${holdId}`, { method: 'DELETE' }, studentId),

  // #8: GET /me/holds
  getMyHolds: (studentId: string) =>
    request<MyHoldItem[]>('/me/holds', { method: 'GET' }, studentId),
};
