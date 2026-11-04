const FIXED_ECS_BACK_PATHS: Record<string, string> = {
  '/ecs/kalender-curah-hujan': '/ecs',
}

export function getBreadcrumbBackPath(pathname: string): string | null {
  return FIXED_ECS_BACK_PATHS[pathname] ?? null
}
