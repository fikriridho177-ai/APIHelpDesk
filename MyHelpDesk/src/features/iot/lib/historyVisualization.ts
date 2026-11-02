export type VisualizationKind = 'trend' | 'rainfall' | 'direction' | 'level' | 'intensity'

export function getVisualizationKind(parameterCode: string, parameterName = ''): VisualizationKind {
  const searchable = `${parameterCode} ${parameterName}`.toLocaleUpperCase('id-ID')
  if (/RAIN|RAINFALL|CURAH|HUJAN|PRECIP/.test(searchable)) return 'rainfall'
  if (/WIND_DIRECTION|WIND_DIR|WIND_DEGREE|DIRECTION|ARAH.*ANGIN|ANGIN.*ARAH/.test(searchable)) return 'direction'
  if (/TMAK|TMAT|TINGGI MUKA AIR|WATER LEVEL|LEVEL AIR/.test(searchable)) return 'level'
  if (/RADIASI|RADIATION|PENYINARAN|EVAPOTRANSPIRASI|SOLAR/.test(searchable)) return 'intensity'
  return 'trend'
}
