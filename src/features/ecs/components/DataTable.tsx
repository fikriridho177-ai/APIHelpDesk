import type { ReactNode } from 'react'
import styles from './DataTable.module.css'

export interface Column<T> {
  key: string
  header: string
  render: (row: T, index: number) => ReactNode
}

interface DataTableProps<T> {
  columns: Column<T>[]
  data: T[]
  loading?: boolean
  emptyIcon?: string
  emptyText?: string
}

export default function DataTable<T>({
  columns,
  data,
  loading,
  emptyIcon = '📋',
  emptyText = 'Tidak ada data',
}: DataTableProps<T>) {
  return (
    <div className={styles.wrap}>
      <table className={styles.table}>
        <thead>
          <tr>
            {columns.map(col => (
              <th key={col.key}>{col.header}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {loading ? (
            Array.from({ length: 5 }).map((_, i) => (
              <tr key={i}>
                {columns.map(col => (
                  <td key={col.key}>
                    <div className={styles.skeletonRow} />
                  </td>
                ))}
              </tr>
            ))
          ) : data.length === 0 ? (
            <tr>
              <td colSpan={columns.length}>
                <div className={styles.empty}>
                  <span className={styles.emptyIcon}>{emptyIcon}</span>
                  <p>{emptyText}</p>
                </div>
              </td>
            </tr>
          ) : (
            data.map((row, i) => (
              <tr key={i}>
                {columns.map(col => (
                  <td key={col.key}>{col.render(row, i)}</td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  )
}
