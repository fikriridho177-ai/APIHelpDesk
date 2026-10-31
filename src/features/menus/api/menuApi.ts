import { http } from '../../../api/http'

import type {
  MenuItem,
  SaveMenuRequest, NavigationMenuItem,
} from '../types/menu'


export async function getMenus(): Promise<MenuItem[]> {
  const response = await http.get<MenuItem[]>(
    '/utility/menus',
  )

  return response.data
}


export async function getMenu(
  id: number,
): Promise<MenuItem> {
  const response = await http.get<MenuItem>(
    `/utility/menus/${id}`,
  )

  return response.data
}

export async function getNavigationMenus():
  Promise<NavigationMenuItem[]> {

  const response =
    await http.get<NavigationMenuItem[]>(
      '/utility/menus/navigation',
    )

  return response.data
}

export async function createMenu(
  payload: SaveMenuRequest,
): Promise<MenuItem> {
  const response = await http.post<MenuItem>(
    '/utility/menus',
    payload,
  )

  return response.data
}


export async function updateMenu(
  id: number,
  payload: SaveMenuRequest,
): Promise<void> {
  await http.put(
    `/utility/menus/${id}`,
    payload,
  )
}


export async function disableMenu(
  id: number,
): Promise<void> {
  await http.delete(
    `/utility/menus/${id}`,
  )
}