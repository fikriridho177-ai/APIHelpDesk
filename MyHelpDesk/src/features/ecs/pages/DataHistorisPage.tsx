import IoTMonitoringPage from '../../iot/pages/IoTMonitoringPage'

export default function DataHistorisPage() {
  return (
    <IoTMonitoringPage
      menuCode="WAD_ECS_DATA_HISTORIS"
      permissionCode="WAD_ECS_STASIUN"
      title="Riwayat Data Sensor"
      subtitle="Buka pembacaan lampau per logger dengan agregasi yang sesuai periode, tanpa menarik seluruh data mentah sekaligus."
      eyebrow="WAD / SENSOR HISTORY"
    />
  )
}
