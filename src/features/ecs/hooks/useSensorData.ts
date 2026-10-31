import { useState, useCallback } from 'react'
import type { DateRange, CurahHujanItem, TmakItem, KelembapanItem } from '../types'
import { getCurahHujan, getTmak, getKelembapan } from '../services/api'

export interface SensorData {
  curahHujan: CurahHujanItem[]
  tmak: TmakItem[]
  kelembapan: KelembapanItem[]
}

export function useSensorData() {
  const [data, setData] = useState<SensorData>({ curahHujan: [], tmak: [], kelembapan: [] })
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // const fetchAll = useCallback(async (range: DateRange) => {
  //   setLoading(true); setError(null)
  //   const [ch, tmak, kel] = await Promise.allSettled([
  //     getCurahHujan(range.from, range.to, userId),
  //     getTmak(range.from, range.to),
  //     getKelembapan(range.from, range.to),
  //   ])
  //   setData({
  //     curahHujan: ch.status === 'fulfilled' ? ch.value : [],
  //     tmak: tmak.status === 'fulfilled' ? tmak.value : [],
  //     kelembapan: kel.status === 'fulfilled' ? kel.value : [],
  //   })
  //   if (ch.status === 'rejected' && tmak.status === 'rejected') setError('Gagal memuat data dari server')
  //   setLoading(false)
  // }, [userId])
 const fetchAll = useCallback(async (range: DateRange) => {
  setLoading(true)
  setError(null)

  const [ch, tmak, kel] = await Promise.allSettled([
    getCurahHujan(range.from, range.to),
    getTmak(range.from, range.to),
    getKelembapan(range.from, range.to),
  ])

  console.log('CURAH HUJAN RESULT:', ch)

  if (ch.status === 'fulfilled') {
    console.log('DATA CURAH HUJAN:', ch.value)
    console.table(ch.value)
  } else {
    console.error('ERROR CURAH HUJAN:', ch.reason)
  }

  setData({
    curahHujan: ch.status === 'fulfilled' ? ch.value : [],
    tmak: tmak.status === 'fulfilled' ? tmak.value : [],
    kelembapan: kel.status === 'fulfilled' ? kel.value : [],
  })

  if (
    ch.status === 'rejected' &&
    tmak.status === 'rejected'
  ) {
    setError('Gagal memuat data dari server')
  }

  setLoading(false)
}, [])

const fetchTMAK = useCallback(async (range: DateRange) => {
  setLoading(true)
  setError(null)

  try {
    const d = await getTmak(range.from, range.to)

    setData(prev => ({
      ...prev,
      tmak: d,
    }))
  } catch (error) {
    console.error('TMAK error:', error)
    setError('Gagal memuat data TMAK')
  } finally {
    setLoading(false)
  }
}, [])

const fetchKel = useCallback(async (range: DateRange) => {
  setLoading(true)
  setError(null)

  try {
    const d = await getKelembapan(
      range.from,
      range.to,
    )

    setData(prev => ({
      ...prev,
      kelembapan: d,
    }))
  } catch (error) {
    console.error('Kelembapan error:', error)
    setError('Gagal memuat data kelembapan')
  } finally {
    setLoading(false)
  }
}, [])

const fetchCH = useCallback(async (range: DateRange) => {
  setLoading(true)
  setError(null)

  try {
    const d = await getCurahHujan(
      range.from,
      range.to,
    )

    console.log('FETCH CH:', d)
    console.table(d)

    setData(prev => ({
      ...prev,
      curahHujan: d,
    }))
  } catch (error) {
    console.error(
      'Gagal memuat curah hujan:',
      error,
    )

    setError(
      'Gagal memuat data curah hujan',
    )
  } finally {
    setLoading(false)
  }
}, [])
  return { data, loading, error, fetchAll, fetchCH, fetchTMAK, fetchKel }
}
