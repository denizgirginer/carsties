import NextAuth, { NextAuthOptions } from "next-auth"
import DuendeIdentityServer6 from 'next-auth/providers/duende-identity-server6';

export const authOptions: NextAuthOptions = {
    session: {
        strategy: 'jwt',
        maxAge: 3600*24*30, //max Age
    },
    secret: process.env.NEXT_AUTH_SECRET,
    providers: [
        DuendeIdentityServer6({
            id: 'id-server',
            clientId: 'nextApp',
            clientSecret: process.env.CLIENT_SECRET!,
            issuer: process.env.ID_URL,
            authorization: {params: {scope: 'openid profile auctionApp'}},
            idToken: true
        })
    ],
    callbacks: {
        async jwt({token, profile, account}) {
            if (profile) {
                token.username = profile.username
            }
            if (account) {
                token.access_token = account.access_token
            }

            console.log(token.access_token)
            return token;
        },
        async session({session, token}) {
            if (token) {
                session.user.username = token.username
                session.user.token = token.access_token;
            }
            return session;
        }
    }
}