export type AuthUser={id:number;userId:string;name:string;group:string;mustChangePassword:boolean};
export type LoginResponse={token:string}&AuthUser;
