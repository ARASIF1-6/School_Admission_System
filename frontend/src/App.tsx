import React, { useState, useEffect, useCallback } from 'react';
import { api, ApiError } from './api';
import type { ClassItem, HoldResponse, MyHoldItem } from './api';
import './App.css';

export function App() {
  // 1. Student Identity (17-digit Birth Registration Number) - saved in localStorage
  const [studentId, setStudentId] = useState<string>(() => {
    return localStorage.getItem('school_admission_student_id') || '20152691234567890';
  });

  // 2. Class List State
  const [classes, setClasses] = useState<ClassItem[]>([]);
  const [loadingClasses, setLoadingClasses] = useState(false);
  const [autoRefresh, setAutoRefresh] = useState(true);

  // 3. New Class Form State (Admin)
  const [newClassName, setNewClassName] = useState('');
  const [newTotalSeats, setNewTotalSeats] = useState<number>(120);
  const [creatingClass, setCreatingClass] = useState(false);
  const [showAdminForm, setShowAdminForm] = useState(true);

  // 4. Active Hold State (Active Application) - saved in localStorage
  const [activeHold, setActiveHold] = useState<HoldResponse | null>(null);
  const [activeHoldClassName, setActiveHoldClassName] = useState<string>('');
  const [remainingSeconds, setRemainingSeconds] = useState<number | null>(null);
  const [processingHold, setProcessingHold] = useState(false);

  // 5. My Holds List (Student Applications)
  const [myHolds, setMyHolds] = useState<MyHoldItem[]>([]);
  const [loadingMyHolds, setLoadingMyHolds] = useState(false);

  // 6. Notifications & Error Alerts
  const [alert, setAlert] = useState<{ type: 'success' | 'error' | 'warning' | 'info'; message: string } | null>(null);

  // Save studentId to localStorage
  const handleStudentIdChange = (val: string) => {
    const cleaned = val.replace(/\D/g, '').slice(0, 17);
    setStudentId(cleaned);
    localStorage.setItem('school_admission_student_id', cleaned);
  };

  const showAlert = (type: 'success' | 'error' | 'warning' | 'info', message: string) => {
    setAlert({ type, message });
    setTimeout(() => {
      setAlert((curr) => (curr?.message === message ? null : curr));
    }, 6000);
  };

  // Fetch Class List
  const fetchClasses = useCallback(async (isBackground = false) => {
    if (!isBackground) setLoadingClasses(true);
    try {
      const data = await api.getClasses();
      setClasses(data);
    } catch (err: any) {
      if (!isBackground) {
        showAlert('error', err.message || 'ক্লাসের তালিকা লোড করতে ব্যর্থ হয়েছে');
      }
    } finally {
      if (!isBackground) setLoadingClasses(false);
    }
  }, []);

  // Fetch My Holds
  const fetchMyHolds = useCallback(async () => {
    if (studentId.length !== 17) {
      setMyHolds([]);
      return;
    }
    setLoadingMyHolds(true);
    try {
      const data = await api.getMyHolds(studentId);
      setMyHolds(data);

      const ongoing = data.find((h) => h.status === 'HELD');
      if (ongoing && (!activeHold || activeHold.holdId !== ongoing.holdId)) {
        setActiveHold({
          holdId: ongoing.holdId,
          classId: ongoing.classId,
          studentId: studentId,
          status: 'HELD',
          expiresAt: ongoing.expiresAt,
        });
        setActiveHoldClassName(ongoing.className);
      }
    } catch (err: any) {
      console.warn('Failed to load my holds:', err.message);
    } finally {
      setLoadingMyHolds(false);
    }
  }, [studentId, activeHold]);

  // Initial load
  useEffect(() => {
    fetchClasses();
  }, [fetchClasses]);

  // Load student's holds whenever studentId changes or on startup
  useEffect(() => {
    fetchMyHolds();
  }, [studentId]);

  // Auto-refresh classes every 4 seconds
  useEffect(() => {
    if (!autoRefresh) return;
    const interval = setInterval(() => {
      fetchClasses(true);
    }, 4000);
    return () => clearInterval(interval);
  }, [autoRefresh, fetchClasses]);

  // Timer calculation for Active Hold
  useEffect(() => {
    if (!activeHold || activeHold.status !== 'HELD') {
      setRemainingSeconds(null);
      return;
    }

    const updateTimer = () => {
      const expiresTime = new Date(activeHold.expiresAt).getTime();
      const now = new Date().getTime();
      const diffSec = Math.floor((expiresTime - now) / 1000);

      if (diffSec <= 0) {
        setRemainingSeconds(0);
        setActiveHold((prev) => (prev ? { ...prev, status: 'EXPIRED' } : null));
        showAlert('warning', 'আবেদনের মেয়াদ শেষ হয়ে গেছে! আসনটি উন্মুক্ত করা হয়েছে (Hold has expired).');
        fetchClasses(true);
        fetchMyHolds();
      } else {
        setRemainingSeconds(diffSec);
      }
    };

    updateTimer();
    const timerInterval = setInterval(updateTimer, 1000);
    return () => clearInterval(timerInterval);
  }, [activeHold, fetchClasses, fetchMyHolds]);

  // Create New Class Handler (Admin)
  const handleCreateClass = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newClassName.trim()) {
      showAlert('error', 'ক্লাসের নাম দিন (Class name is required)');
      return;
    }
    if (newTotalSeats <= 0) {
      showAlert('error', 'আসন সংখ্যা শূন্যের চেয়ে বড় হতে হবে (Total seats must be greater than 0)');
      return;
    }

    setCreatingClass(true);
    try {
      const created = await api.createClass(newClassName.trim(), newTotalSeats);
      showAlert('success', `নতুন ক্লাস "${created.name}" (${created.totalSeats} আসন) সফলভাবে তৈরি হয়েছে!`);
      setNewClassName('');
      setNewTotalSeats(120);
      await fetchClasses();
    } catch (err: any) {
      showAlert('error', err.message || 'ক্লাস তৈরি করতে সমস্যা হয়েছে');
    } finally {
      setCreatingClass(false);
    }
  };

  // Apply / Hold Seat Handler
  const handleApplyHold = async (cls: ClassItem) => {
    if (studentId.length !== 17) {
      showAlert('error', 'আবেদন করার আগে উপরে ১৭ অঙ্কের সঠিক জন্ম নিবন্ধন নম্বর লিখুন (Please enter 17-digit birth registration number)');
      return;
    }

    if (cls.availableSeats <= 0) {
      showAlert('error', 'এই ক্লাসে কোনো খালি আসন নেই (No seats available)');
      return;
    }

    setProcessingHold(true);
    try {
      const hold = await api.createHold(cls.id, studentId);
      setActiveHold(hold);
      setActiveHoldClassName(cls.name);
      showAlert('success', `আবেদন সফল হয়েছে! "${cls.name}"-এর জন্য আপনার একটি আসন নির্দিষ্ট সময়ের জন্য Hold করা হয়েছে।`);
      await fetchClasses(true);
      await fetchMyHolds();
    } catch (err: any) {
      if (err instanceof ApiError && err.statusCode === 409) {
        showAlert('error', `আবেদন ব্যর্থ: ${err.message} (আসন শেষ অথবা এই ক্লাসে আপনার ইতোমধ্যে একটি আবেদন রয়েছে)`);
      } else {
        showAlert('error', err.message || 'আবেদন সম্পন্ন করা সম্ভব হয়নি');
      }
    } finally {
      setProcessingHold(false);
    }
  };

  // Confirm Admission ("ভর্তি ফি দিন") Handler
  const handleConfirmHold = async () => {
    if (!activeHold) return;
    if (studentId.length !== 17) {
      showAlert('error', '১৭ অঙ্কের জন্ম নিবন্ধন নম্বর প্রয়োজন');
      return;
    }

    setProcessingHold(true);
    try {
      await api.confirmHold(activeHold.holdId, studentId);
      setActiveHold((prev) => (prev ? { ...prev, status: 'CONFIRMED' } : null));
      showAlert('success', 'ভর্তি ফি জমা সফল হয়েছে! আপনার ভর্তি নিশ্চিত করা হয়েছে (Admission Confirmed).');
      await fetchClasses(true);
      await fetchMyHolds();
    } catch (err: any) {
      showAlert('error', err.message || 'ভর্তি নিশ্চিত করতে ব্যর্থ হয়েছে');
    } finally {
      setProcessingHold(false);
    }
  };

  // Cancel Hold ("বাতিল") Handler
  const handleCancelHold = async () => {
    if (!activeHold) return;
    if (!window.confirm('আপনি কি নিশ্চিত যে আপনি এই আবেদনটি বাতিল করতে চান? আসনটি অন্যদের জন্য খুলে দেওয়া হবে।')) {
      return;
    }

    setProcessingHold(true);
    try {
      await api.cancelHold(activeHold.holdId, studentId);
      setActiveHold((prev) => (prev ? { ...prev, status: 'CANCELLED' } : null));
      showAlert('info', 'আবেদন বাতিল করা হয়েছে। আসনটি পুনরায় উন্মুক্ত করা হয়েছে।');
      await fetchClasses(true);
      await fetchMyHolds();
    } catch (err: any) {
      showAlert('error', err.message || 'আবেদন বাতিল করতে সমস্যা হয়েছে');
    } finally {
      setProcessingHold(false);
    }
  };

  // Select a hold from history to view in active card
  const handleSelectHold = (item: MyHoldItem) => {
    setActiveHold({
      holdId: item.holdId,
      classId: item.classId,
      studentId: studentId,
      status: item.status,
      expiresAt: item.expiresAt,
    });
    setActiveHoldClassName(item.className);
    window.scrollTo({ top: 300, behavior: 'smooth' });
  };

  const setTestStudent = (id: string) => {
    handleStudentIdChange(id);
    showAlert('info', `শিক্ষার্থীর আইডি পরিবর্তন করা হয়েছে: ${id}`);
  };

  const formatTimer = (sec: number | null) => {
    if (sec === null || sec < 0) return '--:--';
    const m = Math.floor(sec / 60);
    const s = sec % 60;
    return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  };

  return (
    <div className="admission-container">
      {/* Top Banner / Navbar */}
      <header className="admission-header">
        <div className="header-brand">
          <div className="brand-icon">🎓</div>
          <div>
            <h1>School Admission System</h1>
            <p className="sub-title">অনলাইন স্কুল ভর্তি কার্যক্রম • আগে আসলে আগে পাবেন (First-Come, First-Served)</p>
          </div>
        </div>

        {/* Live Auto-Refresh Status */}
        <div className="header-status">
          <button
            type="button"
            className="live-pill"
            onClick={() => setAutoRefresh(!autoRefresh)}
            style={{ cursor: 'pointer', border: '1px solid #cbd5e1' }}
            title="ক্লিক করে স্বয়ংক্রিয় হালনাগাদ বন্ধ/চালু করুন"
          >
            <span className={`pulse-dot ${autoRefresh ? 'active' : ''}`}></span>
            {autoRefresh ? 'স্বয়ংক্রিয় হালনাগাদ চালু' : 'হালনাগাদ স্থগিত'}
          </button>
          <button
            type="button"
            className="btn-refresh"
            onClick={() => fetchClasses()}
            disabled={loadingClasses}
            title="এখনই রিফ্রেশ করুন"
          >
            🔄 {loadingClasses ? '...' : 'হালনাগাদ'}
          </button>
        </div>
      </header>

      {/* Alert Banner */}
      {alert && (
        <div className={`alert-banner alert-${alert.type}`}>
          <span className="alert-icon">
            {alert.type === 'success' && '✅'}
            {alert.type === 'error' && '❌'}
            {alert.type === 'warning' && '⚠️'}
            {alert.type === 'info' && 'ℹ️'}
          </span>
          <span className="alert-text">{alert.message}</span>
          <button className="alert-close" onClick={() => setAlert(null)}>✕</button>
        </div>
      )}

      {/* Student Identification Bar (No Login Required) */}
      <section className="student-id-card">
        <div className="student-id-header">
          <div className="card-badge">পরিচয় (Student Identity)</div>
          <span className="info-text">লগইন প্রয়োজন নেই। ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর লিখুন:</span>
        </div>

        <div className="student-id-input-row">
          <div className="input-group">
            <span className="input-prefix">🆔</span>
            <input
              type="text"
              className={`student-input ${studentId.length === 17 ? 'valid' : 'invalid'}`}
              placeholder="শিক্ষার্থীর ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর লিখুন (e.g. 20152691234567890)"
              value={studentId}
              onChange={(e) => handleStudentIdChange(e.target.value)}
              maxLength={17}
            />
            <span className={`char-counter ${studentId.length === 17 ? 'complete' : ''}`}>
              {studentId.length}/17 অঙ্ক
            </span>
          </div>

          <div className="presets-group">
            <span className="preset-label">টেস্ট আইডি:</span>
            <button type="button" className="preset-btn" onClick={() => setTestStudent('20152691234567890')}>
              শিক্ষার্থী ১
            </button>
            <button type="button" className="preset-btn" onClick={() => setTestStudent('20152691234567891')}>
              শিক্ষার্থী ২
            </button>
            <button type="button" className="preset-btn" onClick={() => setTestStudent('20152691234567892')}>
              শিক্ষার্থী ৩
            </button>
          </div>
        </div>

        {studentId.length > 0 && studentId.length < 17 && (
          <p className="validation-warning">
            ⚠️ জন্ম নিবন্ধন নম্বর অবশ্যই ১৭ অঙ্কের হতে হবে (বাকি আছে {17 - studentId.length} অঙ্ক)।
          </p>
        )}
      </section>

      {/* Active Application / Hold Status Card (Screen 3) */}
      {activeHold && (
        <section className={`active-hold-card status-${activeHold.status.toLowerCase()}`}>
          <div className="hold-header">
            <div className="hold-title-box">
              <span className="badge-screen">চলমান আবেদন (Active Application)</span>
              <h2>{activeHoldClassName || activeHold.classId}</h2>
              <span className="hold-id-tag">আবেদন নং: {activeHold.holdId}</span>
            </div>

            <div className="hold-status-box">
              <span className="status-label">বর্তমান অবস্থা:</span>
              <span className={`status-pill pill-${activeHold.status.toLowerCase()}`}>
                {activeHold.status === 'HELD' && '⏳ HOLD-এ আছে'}
                {activeHold.status === 'CONFIRMED' && '🎉 ভর্তি নিশ্চিত (CONFIRMED)'}
                {activeHold.status === 'EXPIRED' && '⏰ মেয়াদ শেষ (EXPIRED)'}
                {activeHold.status === 'CANCELLED' && '🚫 বাতিল (CANCELLED)'}
              </span>
            </div>
          </div>

          {/* Countdown Timer Display */}
          {activeHold.status === 'HELD' && (
            <div className="countdown-box">
              <div className="timer-wrapper">
                <span className="timer-icon">⏱️</span>
                <span className="timer-title">মেয়াদ শেষ হতে বাকি:</span>
                <span className={`timer-digits ${remainingSeconds !== null && remainingSeconds < 60 ? 'urgent' : ''}`}>
                  {formatTimer(remainingSeconds)}
                </span>
              </div>
              <p className="timer-note">
                এই সময়ের মধ্যে ভর্তি ফি প্রদান করে ভর্তি নিশ্চিত করুন। মেয়াদ শেষ হলে আসনটি স্বয়ংক্রিয়ভাবে অন্যের জন্য উন্মুক্ত হয়ে যাবে।
              </p>
            </div>
          )}

          {activeHold.status === 'CONFIRMED' && (
            <div className="confirmed-celebration">
              <span className="celebration-icon">🎊</span>
              <div>
                <h3>অভিনন্দন! আপনার ভর্তি সম্পন্ন হয়েছে।</h3>
                <p>ভর্তি ফি সফলভাবে গৃহীত হয়েছে এবং আপনার আসন নিশ্চিত করা হয়েছে।</p>
              </div>
            </div>
          )}

          {activeHold.status === 'EXPIRED' && (
            <div className="expired-box">
              <span className="expired-icon">⌛</span>
              <div>
                <h3>এই আবেদনের মেয়াদ উত্তীর্ণ হয়ে গেছে।</h3>
                <p>নির্ধারিত সময়ের মধ্যে ভর্তি ফি জমা না দেওয়ায় আসনটি মুক্ত হয়ে গেছে। আপনি চাইলে আবার আবেদন করতে পারেন।</p>
              </div>
            </div>
          )}

          {/* Action Buttons */}
          <div className="hold-actions">
            {activeHold.status === 'HELD' && (
              <>
                <button
                  type="button"
                  className="btn-confirm"
                  onClick={handleConfirmHold}
                  disabled={processingHold || remainingSeconds === 0}
                  title="ভর্তি ফি জমা দিয়ে ভর্তি নিশ্চিত করুন"
                >
                  💳 ভর্তি ফি দিন (Confirm Admission)
                </button>
                <button
                  type="button"
                  className="btn-cancel"
                  onClick={handleCancelHold}
                  disabled={processingHold}
                  title="আবেদন বাতিল করে আসনটি অন্যের জন্য ছেড়ে দিন"
                >
                  ✕ বাতিল করুন
                </button>
              </>
            )}

            {(activeHold.status === 'CONFIRMED' || activeHold.status === 'EXPIRED' || activeHold.status === 'CANCELLED') && (
              <button
                type="button"
                className="btn-secondary"
                onClick={() => setActiveHold(null)}
              >
                পর্দা থেকে সরান (Dismiss)
              </button>
            )}
          </div>
        </section>
      )}

      {/* Main Grid: Class List + Admin Create Class */}
      <div className="content-grid">
        {/* Left Column: Class List (Screen 2) */}
        <div className="classes-column">
          <div className="section-header">
            <div>
              <h2>উপলব্ধ ক্লাস তালিকা (Available Classes)</h2>
              <p className="section-desc">প্রতিটি শিফটের মোট আসন ও বর্তমান অবস্থা (আগে আসলে আগে পাবেন)</p>
            </div>
            <span className="classes-count-badge">{classes.length} টি ক্লাস</span>
          </div>

          {loadingClasses && classes.length === 0 ? (
            <div className="empty-state">
              <span className="spinner">🔄</span>
              <p>ক্লাসের তথ্য লোড হচ্ছে...</p>
            </div>
          ) : classes.length === 0 ? (
            <div className="empty-state">
              <p>কোনো ক্লাস পাওয়া যায়নি। ডানদিকের ফর্ম থেকে নতুন ক্লাস তৈরি করুন।</p>
            </div>
          ) : (
            <div className="classes-list">
              {classes.map((cls) => {
                const total = cls.totalSeats;
                const confirmed = cls.confirmedSeats;
                const held = cls.heldSeats;
                const available = cls.availableSeats;
                const isFull = available <= 0;

                const confirmedPct = Math.min(100, (confirmed / total) * 100);
                const heldPct = Math.min(100 - confirmedPct, (held / total) * 100);

                return (
                  <div key={cls.id} className={`class-card ${isFull ? 'class-full' : ''}`}>
                    <div className="class-card-header">
                      <div>
                        <h3 className="class-title">{cls.name}</h3>
                        <span className="class-id-code">ID: {cls.id}</span>
                      </div>
                      <span className={`seat-badge ${isFull ? 'badge-full' : 'badge-available'}`}>
                        {isFull ? 'আসন পূর্ণ' : `${available} আসন খালি`}
                      </span>
                    </div>

                    {/* Seat Stats Grid */}
                    <div className="seat-stats-grid">
                      <div className="stat-box">
                        <span className="stat-label">মোট আসন</span>
                        <span className="stat-value">{total}</span>
                      </div>
                      <div className="stat-box">
                        <span className="stat-label">Hold-এ আছে</span>
                        <span className="stat-value held">{held}</span>
                      </div>
                      <div className="stat-box">
                        <span className="stat-label">নিশ্চিত ভর্তি</span>
                        <span className="stat-value confirmed">{confirmed}</span>
                      </div>
                      <div className="stat-box">
                        <span className="stat-label">খালি আসন</span>
                        <span className="stat-value available">{available}</span>
                      </div>
                    </div>

                    {/* Progress Bar of Seat Occupancy */}
                    <div className="occupancy-bar" title={`নিশ্চিত: ${confirmed}, Hold: ${held}, খালি: ${available}`}>
                      <div className="bar-confirmed" style={{ width: `${confirmedPct}%` }}></div>
                      <div className="bar-held" style={{ width: `${heldPct}%` }}></div>
                    </div>

                    {/* Action button */}
                    <div className="class-card-footer">
                      <button
                        type="button"
                        className={`btn-apply ${isFull ? 'disabled' : ''}`}
                        onClick={() => handleApplyHold(cls)}
                        disabled={isFull || processingHold}
                      >
                        {isFull ? '❌ আসন পূর্ণ (No Seats)' : '📝 আবেদন করুন (Apply / Hold)'}
                      </button>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* Right Column: Admin Create Class (Screen 1) + My Holds (Screen 4) */}
        <div className="sidebar-column">
          {/* Admin Screen: Class তৈরি */}
          <div className="admin-card">
            <div className="admin-header" onClick={() => setShowAdminForm(!showAdminForm)}>
              <div className="admin-title">
                <span>🛠️</span>
                <h3>নতুন ক্লাস তৈরি (School Admin)</h3>
              </div>
              <button type="button" className="toggle-btn">
                {showAdminForm ? '▲' : '▼'}
              </button>
            </div>

            {showAdminForm && (
              <form onSubmit={handleCreateClass} className="admin-form">
                <p className="admin-help">
                  প্রতিটি শিফট আলাদা একটি ক্লাস হিসেবে তৈরি হয় নিজের আসন সংখ্যা নিয়ে (e.g. "Class 6 – Morning Shift")।
                </p>

                <div className="form-group">
                  <label htmlFor="className">ক্লাসের নাম ও শিফট:</label>
                  <input
                    id="className"
                    type="text"
                    placeholder="e.g. Class 6 - Morning Shift"
                    value={newClassName}
                    onChange={(e) => setNewClassName(e.target.value)}
                    required
                  />
                </div>

                <div className="form-group">
                  <label htmlFor="totalSeats">মোট আসন সংখ্যা:</label>
                  <input
                    id="totalSeats"
                    type="number"
                    min={1}
                    max={5000}
                    value={newTotalSeats}
                    onChange={(e) => setNewTotalSeats(parseInt(e.target.value) || 0)}
                    required
                  />
                </div>

                <button
                  type="submit"
                  className="btn-create-class"
                  disabled={creatingClass}
                >
                  {creatingClass ? 'তৈরি হচ্ছে...' : '➕ ক্লাস সংরক্ষণ করুন'}
                </button>
              </form>
            )}
          </div>

          {/* Student Screen: আমার আবেদন (Screen 4) */}
          <div className="my-holds-card">
            <div className="my-holds-header">
              <div className="my-holds-title">
                <span>📋</span>
                <h3>আমার আবেদন (My Applications)</h3>
              </div>
              <button
                type="button"
                className="btn-icon"
                onClick={fetchMyHolds}
                disabled={loadingMyHolds}
                title="রিফ্রেশ করুন"
              >
                🔄
              </button>
            </div>

            <p className="my-holds-subtitle">
              জন্ম নিবন্ধন নং: <strong>{studentId || '(প্রদান করা হয়নি)'}</strong>
            </p>

            {loadingMyHolds ? (
              <div className="empty-substate">
                <p>আবেদন তালিকা লোড হচ্ছে...</p>
              </div>
            ) : myHolds.length === 0 ? (
              <div className="empty-substate">
                <p>এই শিক্ষার্থীর কোনো আবেদন নেই। যেকোনো ক্লাসে "আবেদন করুন" চাপুন।</p>
              </div>
            ) : (
              <div className="my-holds-list">
                {myHolds.map((item) => (
                  <div
                    key={item.holdId}
                    className={`my-hold-row status-border-${item.status.toLowerCase()}`}
                    onClick={() => handleSelectHold(item)}
                    title="বিস্তারিত দেখতে ক্লিক করুন"
                  >
                    <div className="row-info">
                      <strong>{item.className}</strong>
                      <span className="row-hold-id">আইডি: {item.holdId}</span>
                    </div>

                    <div className="row-status">
                      <span className={`pill-mini pill-${item.status.toLowerCase()}`}>
                        {item.status}
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Footer */}
      <footer className="admission-footer">
        <p>School Admission System • .NET 8 Web API + PostgreSQL + React 18 + Docker</p>
        <p className="tech-badge">Backend: <code>http://localhost:8080</code> • Base API & Automated Testing Ready</p>
      </footer>
    </div>
  );
}

export default App;
