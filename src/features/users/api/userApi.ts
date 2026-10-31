import { http } from '../../../api/http'

import type {
  User,
  CreateUserRequest,
  UpdateUserRequest,
  PKBPayrollEmployee,
} from '../types/user'


export async function getUsers(): Promise<User[]> {
  const response =
    await http.get<User[]>(
      '/utility/users',
    )

  return response.data
}


export async function createUser(
  payload: CreateUserRequest,
): Promise<User> {

  const response =
    await http.post<User>(
      '/utility/users',
      payload,
    )

  return response.data
}


export async function updateUser(
  id: number,
  payload: UpdateUserRequest,
): Promise<void> {

  await http.put(
    `/utility/users/${id}`,
    payload,
  )
}


export async function disableUser(
  id: number,
): Promise<void> {

  await http.delete(
    `/utility/users/${id}`,
  )
}

export async function getPKBPayrollEmployees(): Promise<PKBPayrollEmployee[]> {
  const response = await http.get<PKBPayrollEmployee[]>(
    '/utility/pkb-payroll/employees',
  )

  return response.data
}
