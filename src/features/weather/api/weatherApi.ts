import { http } from '../../../api/http'
import { WEATHER_FORECAST_DAYS, type WeatherForecastResponse, type WeatherQuery } from '../types/weather'

export async function getWeatherForecast(
  query: Required<Pick<WeatherQuery, 'latitude' | 'longitude'>> & Omit<WeatherQuery, 'latitude' | 'longitude'>,
): Promise<WeatherForecastResponse> {
  const { data } = await http.get<WeatherForecastResponse>('/weather/forecast', {
    params: {
      latitude: query.latitude,
      longitude: query.longitude,
      days: query.days ?? WEATHER_FORECAST_DAYS,
      ...(query.area?.trim() ? { area: query.area.trim() } : {}),
    },
    timeout: 20_000,
  })

  return data
}
