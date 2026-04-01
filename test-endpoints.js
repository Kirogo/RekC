const axios = require('axios');

const API_BASE = 'http://localhost:5555/api';
let token = null;

async function login() {
    try {
        console.log('🔐 Attempting login...');
        const response = await axios.post(`${API_BASE}/auth/login`, {
            username: 'michael.mwai',
            password: 'QWERhk@#$'
        });
        
        token = response.data.data.token;
        console.log('✅ Login successful');
        console.log(`Token: ${token.substring(0, 50)}...`);
        return response.data;
    } catch (error) {
        console.error('❌ Login failed:', error.response?.data || error.message);
        return null;
    }
}

async function testEndpoint(method, endpoint, data = null) {
    try {
        const config = {
            headers: {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            }
        };
        
        let response;
        if (method === 'GET') {
            response = await axios.get(`${API_BASE}${endpoint}`, config);
        } else if (method === 'POST') {
            response = await axios.post(`${API_BASE}${endpoint}`, data, config);
        }
        
        console.log(`✅ ${method} ${endpoint} succeeded`);
        console.log(`   Response: ${JSON.stringify(response.data).substring(0, 100)}...`);
        return response.data;
    } catch (error) {
        const status = error.response?.status;
        const message = error.response?.data?.message || error.message;
        console.error(`❌ ${method} ${endpoint} failed (${status}): ${message}`);
        return null;
    }
}

async function runTests() {
    console.log('Starting API tests...\n');
    
    const loginResult = await login();
    if (!loginResult) {
        console.log('Cannot continue without authentication');
        return;
    }
    
    console.log('\n📋 Testing endpoints:\n');
    
    // Test the three dashboard endpoints that were returning 500
    await testEndpoint('GET', '/promises?status=PENDING');
    await testEndpoint('GET', '/transactions/my-transactions');
    await testEndpoint('GET', '/customers/dashboard/stats');
    
    console.log('\n✨ Test suite completed');
}

runTests().catch(console.error);
