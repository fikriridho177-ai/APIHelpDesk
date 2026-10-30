import { FormEvent, useState } from 'react'
import { useNavigate } from 'react-router-dom'

import { login } from '../api/authApi'

export default function LoginPage() {
  const navigate = useNavigate()

  const [userId, setUserId] = useState('admin')
  const [password, setPassword] = useState('Admin@123')
  const [showPassword, setShowPassword] = useState(false)
  const [error, setError] = useState('')

  async function submit(e: FormEvent) {
    e.preventDefault()

    try {
      setError('')

      const result = await login(userId, password)

      localStorage.setItem('token', result.token)
      localStorage.setItem(
        'auth',
        JSON.stringify({
          id: result.id,
          userId: result.userId,
          name: result.name,
          group: result.group,
          mustChangePassword: result.mustChangePassword,
        }),
      )

      // Setelah login selalu tampilkan splash (HUT-57) dulu, lalu otomatis ke dashboard.
      navigate('/splash')
    } catch (err: any) {
      setError(err.response?.data?.message || 'Login gagal')
    }
  }

  return (
    <div className="center">
      <form className="card login" onSubmit={submit}>
        <h1>Login</h1>
        <p>Authentication & Utility Starter</p>

        <label>
          User ID
          <input value={userId} onChange={e => setUserId(e.target.value)} />
        </label>

        <label>
          Password
          <div className="login-password-wrapper">
            <input
              type={showPassword ? 'text' : 'password'}
              value={password}
              onChange={e => setPassword(e.target.value)}
            />
            <button
              type="button"
              className="login-password-eye"
              onClick={() => setShowPassword(prev => !prev)}
              aria-label={showPassword ? 'Sembunyikan password' : 'Tampilkan password'}
            >
              {showPassword ? '🙈' : '👁'}
            </button>
          </div>
        </label>

        {error && <div className="error">{error}</div>}

        <button type="submit">Login</button>

        <small>Default: admin / Admin@123</small>
      </form>
    </div>
  )
}
