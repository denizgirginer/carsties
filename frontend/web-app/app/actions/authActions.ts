import { getServerSession } from "next-auth";
import { authOptions } from "../utils/authOptions";
import { getToken } from "next-auth/jwt";
import {cookies, headers} from 'next/headers';
import { NextApiRequest } from "next";

export async function getSession() {
    return await getServerSession(authOptions);
}

export async function getCurrentUser() {
    try {
        const session = await getSession();

        if (!session) return null;

        return session.user

    } catch (error) {
        return null;
    }
}

export async function getTokenWorkaround() {
    const ses = await getSession();

    if(ses?.user.token) return { access_token:ses.user.token};

    const req = {
        headers: Object.fromEntries(headers() as Headers),
        cookies: Object.fromEntries(
            cookies()
                .getAll()
                .map(c => [c.name, c.value])
        )
    } as NextApiRequest;

    console.log(req)
    return await getToken({req});
}