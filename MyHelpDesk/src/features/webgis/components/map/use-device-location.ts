
import { useEffect, useState, useSyncExternalStore } from "react";
import { createDeviceLocationController } from "../../lib/gis/geolocation";

export function useDeviceLocation() {
  const [controller] = useState(() => createDeviceLocationController());
  const snapshot = useSyncExternalStore(controller.subscribe, controller.getSnapshot, controller.getServerSnapshot);

  // React manages subscribe/unsubscribe. stop() is reusable during Strict Mode's
  // effect cleanup/setup cycle; it never requests permission or restarts a watch.
  useEffect(() => () => controller.stop(), [controller]);

  return {
    ...snapshot,
    start: controller.start,
    stop: controller.stop,
    retry: controller.retry,
    setFollowing: controller.setFollowing,
  };
}
