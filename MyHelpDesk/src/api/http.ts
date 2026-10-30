import axios from 'axios'

export const http = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? 'http://localhost:5000/api',
})

// Sisipkan token login ke setiap request.
http.interceptors.request.use(config => {
  const token = localStorage.getItem('token')

  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }

  return config
})

// Token kedaluwarsa / tidak valid (401) -> hapus sesi dan kembali ke halaman login.
http.interceptors.response.use(
  response => response,
  error => {
    if (error.response?.status === 401) {
      localStorage.removeItem('token')
      localStorage.removeItem('auth')

      if (location.pathname !== '/login') {
        location.href = '/login'
      }
    }

    return Promise.reject(error)
  },
)
