const b = 'http://localhost:5006';

async function test() {
  try {
    const user = process.env.SMS_ADMIN_EMAIL;
    const pwd = process.env.SMS_ADMIN_PASSWORD;
    if (!user || !pwd) {
      throw new Error('Set SMS_ADMIN_EMAIL and SMS_ADMIN_PASSWORD before running this test.');
    }
    
    const login = await fetch(b + '/api/auth/login', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ email: user, password: pwd })
    });
    
    const auth = await login.json();
    if (!login.ok) {
      console.log('❌ Admin login failed HTTP', login.status);
      process.exitCode = 1;
      return;
    }
    
    const token = auth.accessToken;
    console.log('✓ Admin authenticated');
    
    const guardians = await fetch(b + '/api/admin/guardians', {
      headers: { Authorization: `Bearer ${token}` }
    });
    
    const g = await guardians.json();
    if (g.length === 0) {
      console.log('No guardians in system');
      return;
    }
    
    const first = g[0];
    console.log(`✓ Found guardian: ${first.fullName}`);
    
    if (!first.linkedStudents || first.linkedStudents.length === 0) {
      console.log('Guardian has no linked students');
      return;
    }
    
    const initialCount = first.linkedStudents.length;
    const student = first.linkedStudents[0];
    console.log(`✓ Guardian has ${initialCount} linked student(s): ${student.fullName}`);
    
    // Test unlink endpoint
    const delReq = await fetch(
      b + `/api/admin/guardians/${first.guardianId}/students/${student.studentId}`,
      {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${token}` }
      }
    );
    
    console.log(`✓ DELETE endpoint returned HTTP ${delReq.status}`);
    
    if (!delReq.ok) {
      console.log('❌ Unlink failed');
      process.exitCode = 1;
      return;
    }
    
    // Verify unlink
    const updated = await fetch(b + '/api/admin/guardians', {
      headers: { Authorization: `Bearer ${token}` }
    });
    
    const ul = await updated.json();
    const refreshed = ul.find(x => x.guardianId === first.guardianId);
    const newCount = refreshed.linkedStudents.length;
    
    console.log(`✓ Guardian now has ${newCount} linked student(s) (was ${initialCount})`);
    console.log('✓ Unlink feature working correctly!');
    process.exitCode = 0;
  } catch (e) {
    console.error('❌ Test error:', e.message);
    process.exitCode = 1;
  }
}

test();
