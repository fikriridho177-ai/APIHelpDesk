import { useCallback, useEffect, useMemo, useRef, useState } from 'react'

import { useMenuPermission } from '../../sensus/hooks/useSensus'
import { getIoTMonitoring, getIoTParameters } from '../../iot/api/iotApi'
import type { IoTMonitoringDevice, IoTMonitoringResponse, IoTParameter } from '../../iot/types/iot'

const MENU_CODE = 'DASHBOARD_IOT'
const MONITORING_ERROR = 'Data monitoring IoT gagal dimuat. Periksa koneksi API lalu coba lagi.'
const PARAMETERS_ERROR = 'Metadata parameter belum dapat dimuat. Kode parameter tetap ditampilkan.'

export interface IoTMapMonitoringState {
  permissionLoaded: boolean
  canRead: boolean
  monitoring: IoTMonitoringResponse | null
  selectedLoggerId: number | null
  selectedDevice: IoTMonitoringDevice | null
  detailOpen: boolean
  parameters: IoTParameter[]
  loading: boolean
  parametersLoading: boolean
  error: string | null
  parametersError: string | null
  refresh: () => void
  selectLogger: (loggerId: number) => void
  closeDetail: () => void
}

export function useIoTMapMonitoring(): IoTMapMonitoringState {
  const { permission, loaded: permissionLoaded } = useMenuPermission(MENU_CODE)
  const canRead = !!permission?.canRead
  const [monitoring, setMonitoring] = useState<IoTMonitoringResponse | null>(null)
  const [selectedLoggerId, setSelectedLoggerId] = useState<number | null>(null)
  const [detailOpen, setDetailOpen] = useState(false)
  const [parameters, setParameters] = useState<IoTParameter[]>([])
  const [loading, setLoading] = useState(true)
  const [parametersLoading, setParametersLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [parametersError, setParametersError] = useState<string | null>(null)
  const monitoringRequestRef = useRef(0)
  const parameterRequestRef = useRef(0)
  const mountedRef = useRef(true)

  useEffect(() => {
    mountedRef.current = true
    return () => {
      mountedRef.current = false
      monitoringRequestRef.current += 1
      parameterRequestRef.current += 1
    }
  }, [])

  const loadMonitoring = useCallback(async () => {
    if (!permissionLoaded || !canRead || !mountedRef.current) return

    const requestId = ++monitoringRequestRef.current
    setLoading(true)
    setError(null)

    try {
      const response = await getIoTMonitoring()
      if (!mountedRef.current || requestId !== monitoringRequestRef.current) return

      setMonitoring(response)
      setSelectedLoggerId(current => {
        if (current !== null && response.devices.some(device => device.loggerId === current)) return current
        setDetailOpen(false)
        return null
      })
    } catch (loadError) {
      if (!mountedRef.current || requestId !== monitoringRequestRef.current) return
      console.error('Gagal memuat monitoring IoT berbasis peta:', loadError)
      setMonitoring(null)
      setSelectedLoggerId(null)
      setDetailOpen(false)
      setParameters([])
      setParametersError(null)
      setError(MONITORING_ERROR)
    } finally {
      if (mountedRef.current && requestId === monitoringRequestRef.current) setLoading(false)
    }
  }, [canRead, permissionLoaded])

  useEffect(() => {
    if (!permissionLoaded) return
    if (canRead) {
      void loadMonitoring()
      return
    }

    monitoringRequestRef.current += 1
    setLoading(false)
  }, [canRead, loadMonitoring, permissionLoaded])

  const selectedDevice = useMemo(
    () => monitoring?.devices.find(device => device.loggerId === selectedLoggerId) ?? null,
    [monitoring?.devices, selectedLoggerId],
  )

  useEffect(() => {
    if (!selectedDevice) setDetailOpen(false)
  }, [selectedDevice])

  useEffect(() => {
    const requestId = ++parameterRequestRef.current
    setParameters([])
    setParametersError(null)

    if (!selectedDevice) {
      setParametersLoading(false)
      return undefined
    }

    setParametersLoading(true)
    getIoTParameters(selectedDevice.loggerId)
      .then(response => {
        if (mountedRef.current && requestId === parameterRequestRef.current) setParameters(response.parameters)
      })
      .catch(loadError => {
        if (!mountedRef.current || requestId !== parameterRequestRef.current) return
        console.error('Gagal memuat metadata parameter logger:', loadError)
        setParametersError(PARAMETERS_ERROR)
      })
      .finally(() => {
        if (mountedRef.current && requestId === parameterRequestRef.current) setParametersLoading(false)
      })

    return undefined
  }, [selectedDevice])

  const refresh = useCallback(() => {
    void loadMonitoring()
  }, [loadMonitoring])

  const selectLogger = useCallback((loggerId: number) => {
    setSelectedLoggerId(loggerId)
    setDetailOpen(true)
  }, [])

  const closeDetail = useCallback(() => {
    setDetailOpen(false)
  }, [])

  return {
    permissionLoaded,
    canRead,
    monitoring,
    selectedLoggerId,
    selectedDevice,
    detailOpen,
    parameters,
    loading,
    parametersLoading,
    error,
    parametersError,
    refresh,
    selectLogger,
    closeDetail,
  }
}
