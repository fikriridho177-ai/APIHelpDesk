import { http } from '../../../api/http'
import type { WeatherForecastResponse, WeatherQuery } from '../types/weather'

export async function getWeatherForecast(
  query: Required<Pick<WeatherQuery, 'latitude' | 'longitude'>> & Omit<WeatherQuery, 'latitude' | 'longitude'>,
): Promise<WeatherForecastResponse> {
  const { data } = await http.get<WeatherForecastResponse>('/weather/forecast', {
    params: {
      latitude: query.latitude,
      longitude: query.longitude,
      days: query.days ?? 10,
      ...(query.area?.trim() ? { area: query.area.trim() } : {}),
    },
    timeout: 20_000,
  })

  return data
}
