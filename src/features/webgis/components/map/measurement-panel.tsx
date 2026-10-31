import type {
  AreaUnit,
  Coordinate,
  LengthUnit,
  MeasureMode,
} from "../../lib/gis/types";
import {
  formatArea,
  formatLength,
  lineMeters,
  polygonSquareMeters,
} from "../../lib/gis/measurement";
import { Icon } from "../../components/ui/icon";
export function MeasurementPanel({
  mode,
  points,
  lengthUnit,
  areaUnit,
  finished,
  onLengthUnit,
  onAreaUnit,
  onUndo,
  onClear,
  onFinish,
  onRemove,
}: {
  mode: MeasureMode;
  points: Coordinate[];
  lengthUnit: LengthUnit;
  areaUnit: AreaUnit;
  finished: boolean;
  onLengthUnit: (unit: LengthUnit) => void;
  onAreaUnit: (unit: AreaUnit) => void;
  onUndo: () => void;
  onClear: () => void;
  onFinish: () => void;
  onRemove: (index: number) => void;
}) {
  if (mode === "none")
    return (
      <section className="gis-panel">
        <h2>Pengukuran</h2>
        <p className="gis-help">
          Pilih Ukur jarak atau Ukur luas di atas peta, lalu tentukan titik pengukuran.
        </p>
      </section>
    );
  const isArea = mode === "polygon";
  const ready = points.length >= (isArea ? 3 : 2);
  return (
    <section className="gis-panel gis-measure" aria-labelledby="measure-title">
      <h2 id="measure-title">
        <Icon name={isArea ? "polygon" : "ruler"} size={18} />
        {isArea ? "Pengukuran luas" : "Pengukuran jarak"}
      </h2>
      <p className="gis-help">
        {finished
          ? "Geser titik bernomor untuk menyesuaikan bentuk, atau lanjutkan menambah titik."
          : `Klik peta untuk menambah minimal ${isArea ? "3" : "2"} titik. Geser titik untuk menyesuaikan.`}
      </p>
      <label className="gis-unit-label">
        Satuan{" "}
        {isArea ? (
          <select
            aria-label="Satuan pengukuran"
            value={areaUnit}
            onChange={(event) => onAreaUnit(event.target.value as AreaUnit)}
          >
            <option value="m²">Meter persegi (m²)</option>
            <option value="ha">Hektar (ha)</option>
            <option value="km²">Kilometer persegi (km²)</option>
          </select>
        ) : (
          <select
            aria-label="Satuan pengukuran"
            value={lengthUnit}
            onChange={(event) => onLengthUnit(event.target.value as LengthUnit)}
          >
            <option value="m">Meter (m)</option>
            <option value="km">Kilometer (km)</option>
          </select>
        )}
      </label>
      <output className="gis-measure-result" aria-live="polite">
        {isArea
          ? formatArea(polygonSquareMeters(points), areaUnit)
          : formatLength(lineMeters(points), lengthUnit)}
      </output>
      <p className="gis-help">
        {points.length} titik ·{" "}
        {isArea
          ? "Hindari sisi poligon yang saling berpotongan."
          : "Jarak mengikuti segmen yang Anda tentukan."}{" "}
        Estimasi di permukaan bumi, bukan hasil pengukuran survei.
      </p>
      <div className="gis-measure-actions">
        <button type="button" disabled={!points.length} onClick={onUndo}>
          Batalkan titik
        </button>
        <button type="button" disabled={!points.length} onClick={onClear}>
          Bersihkan
        </button>
        <button type="button" disabled={!ready} onClick={onFinish}>
          {finished ? "Lanjutkan" : "Selesai"}
        </button>
      </div>
      {points.length > 0 && (
        <details className="gis-point-details">
          <summary>Titik terpilih ({points.length})</summary>
          <ol>
            {points.map((point, index) => (
              <li key={index}>
                <span>
                  {point[1].toFixed(5)}, {point[0].toFixed(5)}
                </span>
                <button
                  type="button"
                  onClick={() => onRemove(index)}
                  aria-label={`Hapus titik ${index + 1}`}
                >
                  <Icon name="x" size={16} />
                </button>
              </li>
            ))}
          </ol>
        </details>
      )}
    </section>
  );
}
