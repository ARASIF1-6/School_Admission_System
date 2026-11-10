import { describe, it } from 'node:test';
import assert from 'node:assert/strict';

// Core logic functions tested in isolation
function validateStudentId(studentId) {
  if (!studentId || typeof studentId !== 'string') return false;
  return studentId.length === 17 && /^\d{17}$/.test(studentId);
}

function calculateAvailableSeats(totalSeats, heldSeats, confirmedSeats) {
  return Math.max(0, totalSeats - (heldSeats + confirmedSeats));
}

function computeHoldRemainingSeconds(expiresAtIsoString, currentTime) {
  const expires = new Date(expiresAtIsoString).getTime();
  const current = currentTime ? new Date(currentTime).getTime() : Date.now();
  const diffSec = Math.floor((expires - current) / 1000);
  return Math.max(0, diffSec);
}

function getEffectiveHoldStatus(status, expiresAtIsoString, currentTime) {
  if (status !== 'HELD') return status;
  const remaining = computeHoldRemainingSeconds(expiresAtIsoString, currentTime);
  return remaining <= 0 ? 'EXPIRED' : 'HELD';
}

describe('Frontend Automated Tests: School Admission Logic', () => {
  describe('Student ID Validation (17-digit birth registration number)', () => {
    it('should validate exact 17-digit numeric string', () => {
      assert.strictEqual(validateStudentId('20152691234567890'), true);
    });

    it('should reject string with less than 17 digits', () => {
      assert.strictEqual(validateStudentId('1234567890'), false);
    });

    it('should reject string with more than 17 digits', () => {
      assert.strictEqual(validateStudentId('20152691234567890123'), false);
    });

    it('should reject non-numeric characters', () => {
      assert.strictEqual(validateStudentId('2015269123456789A'), false);
      assert.strictEqual(validateStudentId('abcdefghijklmnopq'), false);
    });

    it('should reject empty or null input', () => {
      assert.strictEqual(validateStudentId(''), false);
      assert.strictEqual(validateStudentId(null), false);
    });
  });

  describe('Seat Availability Calculation', () => {
    it('should calculate available seats correctly', () => {
      const available = calculateAvailableSeats(120, 15, 80);
      assert.strictEqual(available, 25);
    });

    it('should not return negative available seats when fully occupied', () => {
      const available = calculateAvailableSeats(50, 20, 30);
      assert.strictEqual(available, 0);
    });

    it('should handle zero occupied seats', () => {
      const available = calculateAvailableSeats(100, 0, 0);
      assert.strictEqual(available, 100);
    });
  });

  describe('Hold Expiration & Countdown Timer', () => {
    it('should compute remaining seconds accurately for active hold', () => {
      const now = new Date('2026-11-01T04:00:00Z');
      const expiresAt = '2026-11-01T04:10:00Z'; // 10 minutes later = 600s
      const remaining = computeHoldRemainingSeconds(expiresAt, now);
      assert.strictEqual(remaining, 600);
    });

    it('should return 0 when hold has passed expiration', () => {
      const now = new Date('2026-11-01T04:15:00Z');
      const expiresAt = '2026-11-01T04:10:00Z'; // expired 5 mins ago
      const remaining = computeHoldRemainingSeconds(expiresAt, now);
      assert.strictEqual(remaining, 0);
    });

    it('should dynamically transition HELD to EXPIRED when time runs out', () => {
      const pastTime = new Date('2026-11-01T04:10:01Z');
      const expiresAt = '2026-11-01T04:10:00Z';
      const status = getEffectiveHoldStatus('HELD', expiresAt, pastTime);
      assert.strictEqual(status, 'EXPIRED');
    });

    it('should keep CONFIRMED status intact even if expiration passed', () => {
      const pastTime = new Date('2026-11-01T05:00:00Z');
      const expiresAt = '2026-11-01T04:10:00Z';
      const status = getEffectiveHoldStatus('CONFIRMED', expiresAt, pastTime);
      assert.strictEqual(status, 'CONFIRMED');
    });
  });
});
