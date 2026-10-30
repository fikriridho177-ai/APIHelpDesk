export type MenuType = 'GROUP' | 'MENU' | 'WIDGET'

export interface MenuItem {
  id: number
  parentId?: number | null
  code: string
  name: string
  route?: string | null
  icon?: string | null
  fullName?: string | null
  description?: string | null
  color?: string | null
  menuType: MenuType
  sortOrder: number
  active: boolean
  createdBy?: string | null
  createdAt?: string | null
  updatedBy?: string | null
  updatedAt?: string | null
  children?: MenuItem[]
}

export interface NavigationMenuItem {
  id: number
  parentId?: number | null
  code: string
  name: string
  route?: string | null
  icon?: string | null
  fullName?: string | null
  description?: string | null
  color?: string | null
  menuType: MenuType
  sortOrder: number
}

export interface SaveMenuRequest {
  parentId?: number | null
  code: string
  name: string
  route?: string | null
  icon?: string | null
  fullName?: string | null
  description?: string | null
  color?: string | null
  menuType: MenuType
  sortOrder: number
  active: boolean
}
