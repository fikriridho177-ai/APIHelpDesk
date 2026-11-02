export const WEATHER_FORECAST_DAYS = 14

export interface WeatherQuery {
  latitude: number
  longitude: number
  days?: number
  area?: string
}

export interface WeatherSensorLocation {
  loggerId: number
  loggerCode: string
  namaLogger: string
  lokasi: string | null
  sourceType: string
  latitude: number | null
  longitude: number | null
}

export interface WeatherLocation {
  latitude: number
  longitude: number
  timezone: string
}

export interface WeatherDayForecast {
  date: string
  weatherCode: number | null
  summary: string
  temperatureMaxC: number | null
  temperatureMinC: number | null
  precipitationProbability: number | null
  precipitationMm: number | null
  windMaxKmh: number | null
}

export interface WeatherAlert {
  title: string
  description: string | null
  effective: string | null
  expires: string | null
  area: string | null
  severity: string | null
  urgency: string | null
  certainty: string | null
  url: string | null
}

export interface WeatherForecastResponse {
  location: WeatherLocation
  fetchedAt: string
  forecastSource: string
  alertSource: string
  alertStatus: 'available' | 'unavailable'
  forecast: WeatherDayForecast[]
  alerts: WeatherAlert[]
}
