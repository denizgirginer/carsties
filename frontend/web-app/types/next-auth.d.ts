import {DefaultSession} from 'next-auth';

declare module 'next-auth' {
    interface Session {
        user: {
            id: string
            username: string
            token?: string
        } & DefaultSession['user']
    }

    interface Profile {
        username: string
    }

    interface User {
        username: string
        token?: string;
    }
}

declare module 'next-auth/jwt' {
    interface JWT {
        username: string
        access_token?: string
    }
}