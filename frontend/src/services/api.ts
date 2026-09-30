import axios from 'axios';

// Backend URL'imizi tanımlıyoruz
const API_URL = 'https://localhost:5001/api';

const api = axios.create({
    baseURL: API_URL,
    withCredentials: true // ÖNEMLİ: HttpOnly cookie'lerin (RefreshToken) backend'e otomatik gönderilmesini sağlar!
});

// Memory'de tutacağımız kısa ömürlü Access Token
let currentAccessToken: string | null = null;

export const setAccessToken = (token: string) => {
    currentAccessToken = token;
};

// ==========================================
// 1. REQUEST INTERCEPTOR
// ==========================================
// Her istek çıkmadan önce araya girip Authorization header'ına Access Token ekler
api.interceptors.request.use((config) => {
    if (currentAccessToken) {
        config.headers.Authorization = `Bearer ${currentAccessToken}`;
    }
    return config;
}, (error) => Promise.reject(error));

// ==========================================
// 2. RESPONSE INTERCEPTOR (SILENT REFRESH MANTIĞI)
// ==========================================
// Race condition (aynı anda gelen çoklu istekler) için bayraklar
let isRefreshing = false;
let failedQueue: any[] = [];

const processQueue = (error: any, token: string | null = null) => {
    failedQueue.forEach(prom => {
        if (error) {
            prom.reject(error);
        } else {
            prom.resolve(token);
        }
    });
    failedQueue = [];
};

api.interceptors.response.use(
    (response) => response, // Başarılı cevaplarda doğrudan veriyi dön
    async (error) => {
        const originalRequest = error.config;

        // Eğer 401 (Unauthorized) aldıysak ve bu daha önce retry edilmemiş orijinal bir istekse
        if (error.response?.status === 401 && !originalRequest._retry) {
            
            // Eğer zaten başka bir istek refresh işlemini başlattıysa (Race condition koruması)
            if (isRefreshing) {
                return new Promise(function (resolve, reject) {
                    failedQueue.push({ resolve, reject });
                }).then(token => {
                    originalRequest.headers.Authorization = 'Bearer ' + token;
                    return api(originalRequest); // Kuyruktan çıkıp isteği tekrar at
                }).catch(err => {
                    return Promise.reject(err);
                });
            }

            originalRequest._retry = true;
            isRefreshing = true;

            try {
                // Backend'e gidip Refresh Cookie'sini kullanarak yeni Access Token istiyoruz
                const { data } = await axios.post(`${API_URL}/auth/refresh`, {}, { 
                    withCredentials: true 
                });
                
                const newAccessToken = data.accessToken;
                setAccessToken(newAccessToken);
                
                // Başarılıysak, kuyruktaki bekleyen diğer isteklere "müjdeyi" (yeni token'ı) ver
                processQueue(null, newAccessToken);
                
                // Ve 401 yiyen asıl isteğimizi yeni token ile tekrar gönder
                originalRequest.headers.Authorization = `Bearer ${newAccessToken}`;
                return api(originalRequest);
                
            } catch (refreshError) {
                // Refresh token da ölmüş veya Revoke edilmiş! Kullanıcıyı zorla sistemden at (Logout)
                processQueue(refreshError, null);
                
                // ÖRNEK: window.location.href = '/login'; veya Redux üzerinden state'i sıfırlama
                currentAccessToken = null;
                
                return Promise.reject(refreshError);
            } finally {
                isRefreshing = false;
            }
        }
        
        return Promise.reject(error);
    }
);

export default api;
