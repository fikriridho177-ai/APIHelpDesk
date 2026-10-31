import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'

import ScopeMultiSelect from '../components/ScopeMultiSelect'
import {
  getGroup,
  getGroupScope,
  getPKBPayrollCompanies,
  getPKBPayrollDepartments,
  saveGroupScope,
  type ScopeKind,
} from '../api/groupApi'
import type { Group, GroupScope } from '../types/group'

type Props = {
  kind: ScopeKind
}

const config: Record<ScopeKind, { title: string; allLabel: string }> = {
  companies: {
    title: 'Akses Perusahaan Anak',
    allLabel: 'Semua Perusahaan Anak',
  },
  departments: {
    title: 'Akses Departemen',
    allLabel: 'Semua Departemen',
  },
  'item-sub-categories': {
    title: 'Akses Item Sub Kategori',
    allLabel: 'Semua Item Sub Kategori',
  },
}

export default function GroupScopeAccessPage({ kind }: Props) {
  const navigate = useNavigate()
  const { groupId } = useParams()

  const id = Number(groupId)
  const c = config[kind]

  const [group, setGroup] = useState<Group | null>(null)
  const [scope, setScope] = useState<GroupScope>({
    all: false,
    selectedIds: [],
    options: [],
  })
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)

  // Database menyimpan ID perusahaan/departemen sebagai integer ("01" -> 1),
  // jadi cocokkan kembali ke ID opsi PKBPayroll secara numerik.
  function matchSelectedIds(
    selectedIds: GroupScope['selectedIds'],
    options: GroupScope['options'],
  ): GroupScope['selectedIds'] {
    return selectedIds.map(selectedId => {
      const match = options.find(
        option => Number(option.id) === Number(selectedId),
      )
      return match ? match.id : selectedId
    })
  }

  async function loadScope(): Promise<GroupScope> {
    const groupScope = await getGroupScope(id, kind)

    if (kind === 'companies') {
      const companies = await getPKBPayrollCompanies()
      const options = companies.map(company => ({
        id: company.id,
        code: company.code,
        name: company.name,
        extra: company.shortName,
      }))

      return {
        ...groupScope,
        selectedIds: matchSelectedIds(groupScope.selectedIds, options),
        options,
      }
    }

    if (kind === 'departments') {
      const departments = await getPKBPayrollDepartments()
      const options = departments.map(department => ({
        id: department.id,
        code: department.code,
        name: department.name,
        extra: department.shortName,
      }))

      return {
        ...groupScope,
        selectedIds: matchSelectedIds(groupScope.selectedIds, options),
        options,
      }
    }

    return groupScope
  }

  useEffect(() => {
    let active = true

    async function loadData() {
      try {
        setLoading(true)
        setGroup(null)

        const [groupData, scopeData] = await Promise.all([
          getGroup(id),
          loadScope(),
        ])

        if (!active) return

        setGroup(groupData)
        setScope(scopeData)
      } catch (error: any) {
        if (!active) return

        console.error(
          `Gagal memuat ${kind}:`,
          error?.response?.data ?? error,
        )

        alert(
          error?.response?.data?.detail ||
            error?.response?.data?.message ||
            'Gagal memuat data hak akses.',
        )
      } finally {
        if (active) {
          setLoading(false)
        }
      }
    }

    if (Number.isFinite(id) && id > 0) {
      void loadData()
    } else {
      setLoading(false)
    }

    return () => {
      active = false
    }
  }, [id, kind])

  function handleAllChange(all: boolean) {
    setScope(prev => ({
      ...prev,
      all,
    }))
  }

  function handleSelectedChange(selectedIds: GroupScope['selectedIds']) {
    setScope(prev => ({
      ...prev,
      selectedIds,
    }))
  }

  async function save() {
    if (saving || loading || group?.isSuperAdmin) return

    try {
      setSaving(true)

      await saveGroupScope(id, kind, scope)

      alert(`${c.title} berhasil disimpan.`)

      const updatedScope = await loadScope()
      setScope(updatedScope)
    } catch (error: any) {
      console.error(
        `Gagal menyimpan ${kind}:`,
        error?.response?.data ?? error,
      )

      alert(
        error?.response?.data?.detail ||
          error?.response?.data?.message ||
          `Gagal menyimpan ${c.title}.`,
      )
    } finally {
      setSaving(false)
    }
  }

  function handleBack() {
    navigate('/utility/groups')
  }

  return (
    <section className="group-scope-page">
      <div className="page-header">
        <div>
          <h1>{c.title}</h1>

          <p className="text-muted">
            Nama Hak Akses:{' '}
            <strong>{group?.name ?? '...'}</strong>
          </p>
        </div>

        <button
          type="button"
          className="secondary"
          onClick={handleBack}
        >
          ← Kembali
        </button>
      </div>

      {group?.isSuperAdmin && (
        <div className="permission-info">
          Group ini Super Administrator. Seluruh data scope efektif dapat diakses.
        </div>
      )}

      <div className="card scope-card">
        {loading ? (
          <p className="text-muted">Memuat data hak akses...</p>
        ) : (
          <>
            <ScopeMultiSelect
              all={scope.all}
              allLabel={c.allLabel}
              options={scope.options}
              selectedIds={scope.selectedIds}
              onAllChange={handleAllChange}
              onSelectedChange={handleSelectedChange}
              disabled={group?.isSuperAdmin}
            />

            <div className="form-actions">
              <button
                type="button"
                className="secondary"
                onClick={handleBack}
              >
                Batal
              </button>

              <button
                type="button"
                disabled={saving || group?.isSuperAdmin || !group}
                onClick={save}
              >
                {saving ? 'Menyimpan...' : 'Simpan'}
              </button>
            </div>
          </>
        )}
      </div>
    </section>
  )
}
