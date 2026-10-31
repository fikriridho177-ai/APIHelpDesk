import {http} from '../../../api/http'; import type {LoginResponse} from '../types/auth';
export async function login(userId:string,password:string){return (await http.post<LoginResponse>('/auth/login',{userId,password})).data}
export async function changePassword(oldPassword:string,newPassword:string){return (await http.post('/auth/change-password',{oldPassword,newPassword})).data}
