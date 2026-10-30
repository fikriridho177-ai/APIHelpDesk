import { FormEvent, useState } from 'react'
import { changePassword } from '../api/authApi'

export default function ChangePasswordPage() {

  const [oldPassword, setOldPassword] =
    useState('')

  const [newPassword, setNewPassword] =
    useState('')

  const [confirmPassword, setConfirmPassword] =
    useState('')

  const [showOld, setShowOld] =
    useState(false)

  const [showNew, setShowNew] =
    useState(false)

  const [showConfirm, setShowConfirm] =
    useState(false)

  const [msg, setMsg] =
    useState('')

  const [success, setSuccess] =
    useState(false)

  const [loading, setLoading] =
    useState(false)


  async function submit(
    e: FormEvent,
  ) {
    e.preventDefault()

    setMsg('')
    setSuccess(false)


    if (!oldPassword) {
      setMsg('Password lama wajib diisi')
      return
    }

    if (!newPassword) {
      setMsg('Password baru wajib diisi')
      return
    }

    if (newPassword !== confirmPassword) {
      setMsg('Konfirmasi password tidak sama')
      return
    }


    try {

      setLoading(true)

      await changePassword(
        oldPassword,
        newPassword,
      )


      const auth =
        JSON.parse(
          localStorage.getItem('auth')
          || '{}',
        )

      auth.mustChangePassword = false

      localStorage.setItem(
        'auth',
        JSON.stringify(auth),
      )


      setSuccess(true)

      setMsg(
        'Password berhasil diganti',
      )

      setOldPassword('')
      setNewPassword('')
      setConfirmPassword('')

    }
    catch (error: any) {

      setSuccess(false)

      setMsg(
        error.response?.data?.message
        ||
        'Gagal mengganti password',
      )
    }
    finally {
      setLoading(false)
    }
  }


  return (

    <section className="change-password-page">

      <div className="change-password-header">

        <div>
          <h1>
            Ganti Password
          </h1>

          <p>
            Perbarui password akun Anda
            untuk menjaga keamanan akun.
          </p>
        </div>

      </div>


      <form
        className="change-password-card"
        onSubmit={submit}
      >

        {/* PASSWORD LAMA */}

        <div className="password-field">

          <label>
            Password Lama
          </label>

          <div className="password-input-wrapper">

            <input
              type={
                showOld
                  ? 'text'
                  : 'password'
              }

              value={oldPassword}

              onChange={
                e =>
                  setOldPassword(
                    e.target.value,
                  )
              }

              placeholder="Masukkan password lama"
            />

            <button
              type="button"
              className="password-toggle"

              onClick={() =>
                setShowOld(
                  prev => !prev,
                )
              }
            >
              {showOld ? '🙈' : '👁'}
            </button>

          </div>

        </div>


        {/* PASSWORD BARU */}

        <div className="password-field">

          <label>
            Password Baru
          </label>

          <div className="password-input-wrapper">

            <input
              type={
                showNew
                  ? 'text'
                  : 'password'
              }

              value={newPassword}

              onChange={
                e =>
                  setNewPassword(
                    e.target.value,
                  )
              }

              placeholder="Masukkan password baru"
            />

            <button
              type="button"
              className="password-toggle"

              onClick={() =>
                setShowNew(
                  prev => !prev,
                )
              }
            >
              {showNew ? '🙈' : '👁'}
            </button>

          </div>

        </div>


        {/* KONFIRMASI */}

        <div className="password-field">

          <label>
            Konfirmasi Password
          </label>

          <div className="password-input-wrapper">

            <input
              type={
                showConfirm
                  ? 'text'
                  : 'password'
              }

              value={confirmPassword}

              onChange={
                e =>
                  setConfirmPassword(
                    e.target.value,
                  )
              }

              placeholder="Ulangi password baru"
            />

            <button
              type="button"
              className="password-toggle"

              onClick={() =>
                setShowConfirm(
                  prev => !prev,
                )
              }
            >
              {showConfirm ? '🙈' : '👁'}
            </button>

          </div>

        </div>


        {/* HINT */}

        <div className="password-hint">

          <span>
            Password sebaiknya minimal
            8 karakter dan kombinasi huruf,
            angka, serta simbol.
          </span>

        </div>


        {/* MESSAGE */}

        {msg && (

          <div
            className={
              success
                ? 'password-message success'
                : 'password-message error'
            }
          >

            <span>
              {success ? '✓' : '!'}
            </span>

            {msg}

          </div>

        )}


        {/* ACTION */}

        <div className="password-actions">

          <button
            type="submit"
            className="save-password-button"
            disabled={loading}
          >

            {loading
              ? 'Menyimpan...'
              : 'Simpan Password'}

          </button>

        </div>

      </form>

    </section>

  )
}