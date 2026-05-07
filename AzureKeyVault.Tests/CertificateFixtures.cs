// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault.Tests
{
    /// <summary>
    /// Pre-generated PFX fixtures for unit tests. All are self-signed with CN=test,
    /// password "test", and a 10-year validity.
    /// </summary>
    public static class CertificateFixtures
    {
        public const string PfxPassword = "test";

        /// <summary>RSA 2048-bit key, password "test".</summary>
        public const string Rsa2048Base64 =
            "MIIJuAIBAzCCCW4GCSqGSIb3DQEHAaCCCV8EgglbMIIJVzCCA6oGCSqGSIb3DQEHBqCCA5swggOXAgEA" +
            "MIIDkAYJKoZIhvcNAQcBMF8GCSqGSIb3DQEFDTBSMDEGCSqGSIb3DQEFDDAkBBDI/WMvLuOzZaWPUpCe" +
            "cG0pAgJOIDAMBggqhkiG9w0CCQUAMB0GCWCGSAFlAwQBKgQQTw/l68MeL1qWknma3KcPi4CCAyCf4igx" +
            "aFd7Z6IXtc9U0eUFGG8J+J+tI5gREmZZemioQ7cdHq/I/h7D93za2KhMreX8ACHkUOz8ignfWgxs2bX5" +
            "XMrbpz+jjwlaEfuoXGscI9l7+v/LbbwCCDk3TVyY53H5cbA67Afo6795Sms1NmKxb1ySG2jbcZ4vye6U" +
            "oJ21EHvbtRL8do3o0x6rqIB4uB94ke4C2w7JUPXQRh/9045Ldr06vXXF5YXdyULS4+zM8esujHZ5YJ4q" +
            "XUyLPVpcpLUfIFTExnrnxgkQb9kNDm+/4XaNIBUxg+IF6eoF+LpuQfKJs9ExHEvkpD4+7+OHKnTJe3bd" +
            "Sm2138J42misGUk5qQQD4Qq4ymcX/hBi2afRHvgHhlf0CWURraFbUK8HDqoHYnh5FBDQrNYj/etOzBaQ" +
            "KF02nuPv6v4MDGgPWAFqNsFC72SDVQOqByNGMeZrGwoNp3WvBl9TXypr9stLmD5Vi4RmPyK3GTyJ6hkS" +
            "n79/6K4SLzHw4gMarYCp5DdY9kXKM2iNk466bk2NNFZtzhfcbDP5SyBXKmemoM3xqjPfbsZzQnGMv14I" +
            "76D1Rm9uRsV8382qvMOI2vJfeeRRMGnc50+qm0ROzy+ZuifCwiY/rBFI1Y4yAgKpXgJ/dycdUqZhEurA" +
            "yQ5lnxIV7tyEJtV2EAtnMUIkTlMR8AASCd2bYKWKTKeD74r5rjscitI8iVczm6mhj3IyjAent74lYGJK" +
            "Zb0pSvmzpzjuotG+lDdzntHozpJyejauhHc6CzaThaGtiqj+r/ZwaJaTtm1xu/j6fInb9rP36lnsvrRk" +
            "RjLCdeQmXtmvoqsdMO1B7O310r4uYBJgan7PbG0ce9e6mjZT9xvepu57LprKiREn0RsgTWIE/kiP65J6" +
            "WCE1g8UBl0miEy0HyMYtHxlULXj3nUL3oTZZNf3HK0eMFEiYdqvMKD5MJHZtPKhLBZaGgzjhIxrmb8Zn" +
            "yE+OJph01yEJx42y5B1w4B3qn+xBxxEonAY+ifTAnPEiFLKk1JRHN4vDkptWK8vSkw2cneWSVXCoH36A" +
            "e4UzxN4I2Po7NckWSfzxbjCCBaUGCSqGSIb3DQEHAaCCBZYEggWSMIIFjjCCBYoGCyqGSIb3DQEMCgEC" +
            "oIIFOTCCBTUwXwYJKoZIhvcNAQUNMFIwMQYJKoZIhvcNAQUMMCQEEFFV4u2PAcvioYhmkny5SkkCAk4g" +
            "MAwGCCqGSIb3DQIJBQAwHQYJYIZIAWUDBAEqBBCysM56JZBTpgMgRH8TCv0HBIIE0NNPPtirUdOzLtSg" +
            "pVodZflyYEI4ONYcnZuYcN04Gv4guN8UaOHDBXymu6ubRb5mh8B+yIduXfTE6ciIurA1c1Rwj8jm0JYG" +
            "30tchbaEqXdjlO314hpKd1CST+VtdQhKRCVNGIN4lxB69wTq5VYDfTOlhNKxZyQkHhkcBWzQeHBQq7O0" +
            "Mva+CrI2pZHtx3Jf0g7xgtJTlKGDDLrNE8z4mJIYh+w/lBk7keIL3W45kDByRDLfoZWybdYeODfmSm/e" +
            "NHIeJJ0vqC6VU2NY08A8ZA1SVrf2VL2YJx3vquLSxQPiRO2sXPt9ArqqLlGnlr98VmI55sVnYxH/dpkO" +
            "MSCC3rqTU0m6gMN8mgdzpinsnfiU3iQmc45mYEP0sWbqXdUdWJ2ronwpvYwuMUoC8z8M4WNs6zodIhQp" +
            "wP88K1pHBxugcCZOTml9c8+tRwxokFrTdjtauePJ4CVMxNWCHeAtksvlbRVAmIVlXBOk+bG8crCHt311" +
            "w/z6irQ2eCbPAVAMWUDiZa3JYzxtrShXSmujt+SMxJQVhNcDPdK3JZy0mTFf4ac1PCe5y6NzUfeKhggs" +
            "V4UGBMC7tvaBy6AcZEOzFZ3k1E8RCjGxDNg8mOcEx3F8mPVlzC03lkevgPykDwFK/dSDN0rGJJk35y7s" +
            "N+0gbCn/BsPCtwR1EcdxxZd/ZRCy1SWZ2mcbd3I3lChJXnixKd84uKiO7Fhxyr8/iebnNcFOdKQJ8QUw" +
            "k/3J9aqj/NJydSBPTFR00Uozy4L/SqyRBsDt0B3gltkAG2oTiSdaJ+16VYBtdzHRnQnqg2ZnezfjlQlB" +
            "xBkYVvydCgXcaUvVAVLh6JpEIoMQHTRqNZcniKpFaA6bklj4J7vqETFnnPCuSV5vW5lUZOum/SPSI2/+" +
            "QzDGZ1gLTIeFJQxWWNTZ/+sDzHHaPpKfc/p2MlqoaXxJEG7vVFfgWbaggv5otMPv4/KIjkKCrVeJKIfX" +
            "Ha1UQYWASTQLK1IG8XvWDf5vqPXgK/+leewFYW6Di9DrVIKekVu88SpCrIl1z9esFj19Dzg0Uh+mjVb8" +
            "yekDdWS5BQc77qjGqiJysuVsA88h3JEPlA3YGP11J6Ux8b8AvrqaBb13Ah4EoXI5scVpXj8AUybzle+u" +
            "g5T4Ty6jURRvwyFxaZq0870DhhuwlpCGwgUEk+kpJugoYz6HhWfXPRGJIxc7SHDS4I+gR/F2fofuChk0" +
            "GNGz+S3dyQ19h06eDvKdpFE/I9s0sRquT9CXCWs5LV9qnT/y4GFiwukhrNgrUbJJPPZwkhc64GymZWcK" +
            "fo3/0aYo4JrjOK8npHKjCrnavQQvF+MNED/QLAJ2CY7snxcprl5b8/sQuyTkDKUOwPleAMA3UNcRbkO9" +
            "aj4qrXDy1ZIXIE+1JWU8w+X2Bxfeb8vtwWF2a5cLd+1axwXxhdlCN60Ogkafp8r5Cz0IA1eNqRk+YXrd" +
            "F+wgmhE5CiaCD479m9db18pbzjmQ1hcBBGgywE5FV4crpEh2ISs12fpW/Ty73uLUBPsXGYn03POiljHg" +
            "Zq4J8x4avTp2JBTd23hUBUUrnn971CMg04R2IB1/uxsF01XM6+Nrua+XKE04IBCo+5tcXRsZrmdIj6iX" +
            "uQfFl5HLXe+5VHd5EwsynyDeeArBMT4wFwYJKoZIhvcNAQkUMQoeCAB0AGUAcwB0MCMGCSqGSIb3DQEJ" +
            "FTEWBBRIKGmYLyQyO0rBTw4UWWVxKI/I7jBBMDEwDQYJYIZIAWUDBAIBBQAEIGQSWmYN9FEbTXJxMmpF" +
            "Rvkxk1Snfx5sl2AcBL5pqntCBAgbIbLmlcVpnwICCAA=";

        /// <summary>RSA 4096-bit key, password "test".</summary>
        public const string Rsa4096Base64 =
            "MIIQOAIBAzCCD+4GCSqGSIb3DQEHAaCCD98Egg/bMIIP1zCCBaoGCSqGSIb3DQEHBqCCBZswggWXAgEA" +
            "MIIFkAYJKoZIhvcNAQcBMF8GCSqGSIb3DQEFDTBSMDEGCSqGSIb3DQEFDDAkBBAzLLQ0KtpG99NZcCP7" +
            "uXO8AgJOIDAMBggqhkiG9w0CCQUAMB0GCWCGSAFlAwQBKgQQubgTXXe5E6+jzkOmpjAw/4CCBSAqhKTU" +
            "kEFeUNwqNtI8UIkCYfxkR5ln82hzH7Wd+XE/Yx+fo9NshhlGh3riqzQwqG3+jeMubqIUiN9TpQfZakqe" +
            "fXAGtJlVfFcwEKKn8rXRh+Z1EG7cUFESt59g9jBf9igc6OwAGTt8YgJtJwiF9WwtTCB+Rl228eLVm1n3" +
            "pb6rQyZm6xYSQoaEUbOQN+U6294dE6ejUDxYVvx09vNsoWanhpYz0Jx4W6IQMMQxmdCLINNN0a9A0f2F" +
            "xG4EJpkaQzJDE5bCbBOihoEasRrICnZ0MFvSNKvCAt70jwVNcvX0JOv7ahxXQj6wVHB6pWpthEiOObMn" +
            "1yWB+CNeyegL8F+dGTvJb5GEa/+NkBD3tK+jqsN0uFEWSHcMxFiiKWgXNoALa7XS/qKRfoqhMG9584Dr" +
            "bayNcEwAPGeFoXjtdSKYPu08ka74q6BApmBuXG0cvRx8VAQYrgtaolZRSd70JE+pVybt13MpToqNTufO" +
            "ECiEGpfmzoobU0v3DXaQ79GA0XrXgF4KQ4GQN+kAzceqQz/kyRrBGCBZ7iC4D2aOBKZaTIePTPt/JodG" +
            "VG1MNJ+nlhNXnRC/1cUtQXipActcC5fZas31GWFvjiJ+BOZR8huQrNkLYlVS6Ft3+gRzAU+m4j+nUBM2" +
            "vCVmA/Q6Xu17XiN19ZhbjGV83WCMRszIsKNtL8typGAcWfgRfE7hsXYmMPHjRfitvr+GKDM1FktDlN0k" +
            "WkIMp+GF/xrK1LMCA2ajn33ad5SI7KikXGoOH4Sxlc6MVTKiIJYbCMddzqzVj7nMRI+sRQgP/VwqWdQq" +
            "d9fIcyQmZUDOYN7rG/RRYKcaAHl/ZeicfOxsTIpyzp2OnAQmh4OFfIYO0U91y9LO9HLfzoCkZdFbuSZW" +
            "owzE6MWzYuhX6rHYSNXxgjSIgcFc0JAHIys12mV/sR/wOLRzGrL2dzWagixt5XEdrHMvog7zlXzfFvit" +
            "kol/75RgZKZe1KiWAC0HuwWgqFnQHttwCrLBymbWJTpsKxv2aYoPWkv9dHHEmoV5CwG8J/BEJBi+A6K0" +
            "XPnR43EF2rsuLIdE7sQ1Wf3S7Zx9msZZCCFF6BF6mSMsETERom6dxgToL+7IyWmVlZ/zIpS5IVTMHBd7" +
            "KoW258VRU1pP3Pu1KkJBlyWwH1VPEQ2XCX8+dzZzLM2KBcmLJzUFNs19eQ9/RQarnuyvLnfq9L2Il0kB" +
            "uKJrgvQ6TWT08a9KPtFqGjfSXAFxVzYpWuc3s/bZ0xAQT+uXw5NfjWo1l/NbRAiT5z+l4re6sqLQAcs/" +
            "1rOUzL5clmEeo8hj8jNW98kEsr2yekgkJQlJ7RjkuuIrrwK+PQ48IJrmsvcS1icYHJGkIbe3TefQYM3E" +
            "/jdrDxDYnco9jwH5Rk/oGoo1hsUmIYRJSnU9GCRfATyQiSsiw9fgXCKdlCkYc46UlKnFm5wAcBBwYCmY" +
            "vr1mIoYcTTp9KRkggyXaxs1pGuGdwXCt+oUNunHdF6wD5vM8fYEiSBkwOUWSAoeDvFVlmRUZ/zVr1KXj" +
            "XaklQrtLOcpr39svT+BHvd6t96qSnKBePnlR8cSQ7NhqjNDFGGQ99b1UuBfhJbaXtLTPbl7IOx7ymZH9" +
            "UMhvIIBqSpcnTx5nYDHMkTzwoszxRrCZoaPploUtqJEmMS2z2TF3DM8jLt1gU8T/D5ydnIzMxMhwXjYK" +
            "O3gP+mEsP6S8gozHK/9Mw0KMo/JlS2G583PStSp0xTTrq01zmssG0jubog+IBj/wMIIKJQYJKoZIhvcN" +
            "AQcBoIIKFgSCChIwggoOMIIKCgYLKoZIhvcNAQwKAQKgggm5MIIJtTBfBgkqhkiG9w0BBQ0wUjAxBgkq" +
            "hkiG9w0BBQwwJAQQ7gHUvW3HKZ9c2wfg7TJvNAICTiAwDAYIKoZIhvcNAgkFADAdBglghkgBZQMEASoE" +
            "EKxfeDPuzII7mzmp/eyQH78EgglQKh8mDRRcW+NC+WrvSLTKw3iB4xk3qIVHIejFWTDRoAxxN9vezZ6I" +
            "ffZKUTaL1yiIJrEsL7o4AKIwNCjJtnZGxETcb3jV7vO9jL6IiDEEhQWA6K+vEIG4m1HyrFnOu4AXvb3h" +
            "jBlrlwIsEyDU++lRBtMCKKPlPLdIJY1bW/VsNkub3/QgSk3Yg75+BUeZTP8er5DDd3ME5+r8r9nmB8ez" +
            "bq/A1aGbaURLzTXB26y8dAIwAQR8+Eix4WtKHms39Y0Nm85WMhqsKWQfrTSvpWIpX2A9sirLN5RD1jD4" +
            "7lL0YkaM4iR4xM2RwB/Qb/FNgEI+aJS9oPfQ4tkmilImauY+Qf4m8rEnFFBbemsKfRVzYlVe3Zg2HTIU" +
            "PaitMdKnahSbl+cNLMBlHK9tjo9/ucZVx7ctQCHQHky0OJ8OjR/33uYBCiJmexeN3dwIfweKV5t4pjWM" +
            "xkZiJS94LL2C4EXdLRdJnmEC4zV5aKYRslpGCW3JjSFVSxxIfU17jRFdM4mgUnvl7lonvXDAD4F1XgLd" +
            "5WLPusDrQRhJZU9zNGdphNuvrSoSbcZFLmNkFA7D0A3A2uQgu4HQ3dUfZwRerBP+Nv8QVy8cLGLP+gjq" +
            "5UF5d/OjFgtDkVLE1eCpzED2LovRKtW3mM4HkIfCJ2I9rm7e/MWPOjBhK9CN7MI+sqVVzO0WFeZBY0mW" +
            "vw414MczTGFTvjFr6RGtm2WYrPbmwDWxz/rI8/RPtaRtE51rZMpGa1cI/OLwnrPq1AmnHYAT2e0sBlRs" +
            "0W6rjq8pYdbUB3/glE34cKKAy2uqY7/lQE4qcXKrC05OrC5uWbzfJYYTbmTUV47i3qT7WdqeRF3J5omF" +
            "8W70eC3GtudXzcaI2nlhomDlfvI4y7x9deZu70RKSs6q0tJghzIX/W5njO0kVhSeqndsB4IUsdWZdgJy" +
            "Kkxq2W34ijAHEdkjkz/8tLXg6W/Eoktli48NDD8l77HSghz2amJygsneoVhlvm2dgXV8iH+HPPapJws6" +
            "B8adhFn01JXkztHcfZSaAbcqf6KoRqccJjdivjLtqm33rF4XHbx5lQX7s0Ar4tOQ/BHY6Ls5sMXeOKnL" +
            "K/5dUyqjsskN6CfbTyA4kSybvnt6J1uiO6HarLD+WsA8UElDSeVZ0v6cH7xAX+QNEs7+1SMbnfqLGe5Z" +
            "ObaFLfTdthF2Nch9bYX4pK5To9hhXIRYinECJz1fJ6g2GgtjMCejW73Tin3mej3PCT7gXrvIKJTW86yd" +
            "8KlCDJEfq4BQ9msoYwWYbCzJrBhQ14aXNu1Tebo/cOXRwATGbby44Hqnerx5WrIiGx7GL7sT0Q4ytN49" +
            "H4bu91wi1oKlHuO2h44ddh2hQof1So5OHAc6+Gg4MvIEXLeHbf+Bx0pxU8rpKJmbY5wCZBQGbvbhURth" +
            "8PcnEBm0MvfzQZxLT/9Hu8MTTtYiIQFb0oMCkd7U3ISMNsfJ3ld8aTZUTS41UCjK0S3EAuQQuLgq5u5y" +
            "vW/bKdFiIhA+IgEBj8TTyyuCkLlRQMw8CWg0yob/JR5RMwnEXVdsWlDF16pvpgP2dPSXY4KEB9WL/x11" +
            "BrrqZ6pRLJfL2oS9NlpewdpcIy+0QdQkTnJiBX6A2S48+6sXMqXh/LEMy8TW8Vo9oE4f3FC96egux51O" +
            "0aG6auWoR/KTPwB4yOIB9GZYG517zzAx1R5ejhS6+eWzywtSY8LcCn8mkEk1Qm2sm5JBKMnsLS2ZfZtz" +
            "E9yYjOY+Lzd59PLJTNyqqE/Ok277mBR26A54zvSd7Kl0bc6j0c3Tzufn8VqCpYlno2Eyv1+CPF6MIowi" +
            "X+2SOWLPSLEVLE096S/BWHX6eaNw/OtmKZD0y/hfk9fOE8gALSUjsjWz+7kJsQpsjR1et7D6xamq+mvI" +
            "tZJUgeIYgwao+cKvPQpLA4BeCGbkHHpfwdMW5ps745bGkWur9Vx8/RotnTlQETFHia8fzDpJwjyug9Dw" +
            "7iAjs6SmA0X3eLGJFbPCuJ7iNUuIBiBcD39XXIZgKWBcdHfuWOk5iuoOk/JiS5r52EXt+MtLgmoS5zUC" +
            "QGV/1g7YqpUSduRTNf2e877CDbyO5E4TyzxQ6Hi2j4vK58uXj4tJseIFKJBJ7C6eSAurBKra3V0r/Nwq" +
            "gNSDHBCuxp3gPx/IUg3HYhkudA2Fscf+zDaUbZoe113QVZSXwr0ZG0bpUkKTrU6/3UChjXZYimF4sgjn" +
            "AzyafqI44zmSqIlkYxSmrWaWhu9P5Eqs4IklTHoyL8CoD+lRfvzaO+1Mihcmh1ApHRDiHBl4l2jI4orn" +
            "vO16iI9i4gakrsdfXJFl99jvlP2vY0/X1h0ng9QNtYxa86VkP0jghsN2S4Fh7+wqzN7weT2ByJGV7ISB" +
            "c+uSRyEBvD+6C8U2Vlc3EQtX/ZAiwV1Sx/3tyHU0ZQufTutAxAjnaAPK2v+OJUflpRPuf8/yZ/r8yyS6" +
            "VWXQxFOkk6/qYqOv2exMoFTmsm5MJ8ejGb8bjyz6rcar/nDbCFsU5jIja5ddfchstxi3nfe2/M2M3prh" +
            "zbTjI0p1aVNoj/6bRvMTvsUbdYOL4UeN+WOlxmHci09eoUXVaYKbOSAyHufb6CRLdKgdw5+mBcO3bTd+" +
            "xLECK/tPtY5RpqZbpezMepXWgsXIrC8VooyLWnTXxkjmb3aeN3ZAai3h6GYMiDPR5Cjounw7b6OrkbTC" +
            "NxQsNXTkjPz4zd8Tei8XfP1jKK6ch+T91iKQqC6YK3xWVZi26utFarm+q1/ZIw48rFnQPAtmAupNw1mr" +
            "CFi1XF2kjtVB0ImFZE3NEJIXNuQb5pFZstlXM06pYRptZXIJWT9XEAq9GfBmA0Akapi3/bvoFCMcydGY" +
            "a1b0f0kD4NNiqgnqCHt6tNCeIy/u4cXddMLV390QwX3R5pVkFug9iXThFUmg3ZgA0iOa5xfYMq98MkRy" +
            "+Yq37BiJIe5nEpaDKFKaU+WE39tQLok0pnaLew0cxOjqGt9/3HBQ0LQDKzbM1JGlSgTmaVIw0cgoPtTj" +
            "L19QU/asPzFvs85g8gVKK6DaS5D6k/63znFYPgxrS5aix8JLIeaxPbJnaji1s8sMwz+px2itiHFycs/L" +
            "13Uc+HGQPI9FVdD7x0CWaqkj/1Qmi4QcZsEUE0DXAVyO/yWg0EZXnpUk3eIJ17uN1+Leo/mToZgEPeAq" +
            "FgPJN80xPjAXBgkqhkiG9w0BCRQxCh4IAHQAZQBzAHQwIwYJKoZIhvcNAQkVMRYEFEmkAouDi5BReB7K" +
            "MWQz6LqUu0YVMEEwMTANBglghkgBZQMEAgEFAAQg8h9ZAz5PXqFZRlYwFjaKVQBphQidZEDE9heqP+zb" +
            "VrUECLbuFmx3IYjnAgIIAA==";

        /// <summary>EC P-256 key, password "test".</summary>
        public const string EcP256Base64 =
            "MIID9QIBAzCCA6sGCSqGSIb3DQEHAaCCA5wEggOYMIIDlDCCAioGCSqGSIb3DQEHBqCCAhswggIXAgEA" +
            "MIICEAYJKoZIhvcNAQcBMF8GCSqGSIb3DQEFDTBSMDEGCSqGSIb3DQEFDDAkBBDv8yCHkzvdUE3Yf5Pr" +
            "mggHAgJOIDAMBggqhkiG9w0CCQUAMB0GCWCGSAFlAwQBKgQQ5sV8M1KI9munRJOOtaHYLoCCAaDCymkD" +
            "U+6i5kdjJh2ImDPukY7PF7ZV704AZsf4XHL0p7V14VraNU84sM5FTfjIIL98GRynAgG3CluFYI1/Wx4I" +
            "+apT9daRTSsm7G/5/KlGZUBDhEw1AYLw9F6/hZSAjS8BrOnCQoqsaMf9AqgT0Z1jObYvc7qPFG1/WqqH" +
            "24OO1SeGwZWsFdb/fWU6wbzo6wwWEPSvfa83pBnV/esCFW4EX+/TL6YSs1f0QMfPX4flqBK2uIvkPq2e" +
            "/MjBz4C7ohBUW8kUseeoghVfuqfweOU+51zDFBOzLyPrb6b07GKRfyUyEKpWniulF/y3oCTL9LiYb1tT" +
            "yQ0c+nZlDL1P13Y12XPPuaZsOZ/b9eKG7UqZKx7oPJ4ATnVuu5/Q83+82ZCoIrZaeD/hZ7Ezp2gLcFM3" +
            "u0tqP4ZNLZQBUchTZCILLrEDAIYiyaxYXedPkL3OLm8wGSNUu6sycjKDiTMzAKotyyX6CMhCJmkN+HbN" +
            "dZiUrhNm45syJ4Lhd042GKlKhQBIOkd1Rdq6ma3lxHtSc79eiYcdP7+9PEbEWAfj25f61zCCAWIGCSqG" +
            "SIb3DQEHAaCCAVMEggFPMIIBSzCCAUcGCyqGSIb3DQEMCgECoIH3MIH0MF8GCSqGSIb3DQEFDTBSMDEG" +
            "CSqGSIb3DQEFDDAkBBBt1HypQHJDjdHiqCKR5B4rAgJOIDAMBggqhkiG9w0CCQUAMB0GCWCGSAFlAwQB" +
            "KgQQll2ZmfA8+/pYMUA4CGR1MgSBkLyuPMAIjyOCemT8XA7TrTtZoPhNIgErPweT7PbubSMisSBy6lhY" +
            "Nq06OiZcKCPWJb1Yro1vtSBVeBjsQwj8nSv29nPPU8GwIeVX915Rmy99lGFVG9JTJfFFkiGDEgIYB9va" +
            "tFeAiRMLi1EVd1Kkg4xmMpEhCvxyhKHdWvieU6Qt8Svq6oj3tgFc77efqX8gNzE+MBcGCSqGSIb3DQEJ" +
            "FDEKHggAdABlAHMAdDAjBgkqhkiG9w0BCRUxFgQU8ICl9s/YnYxF58wsmg02r6ISuYowQTAxMA0GCWCG" +
            "SAFlAwQCAQUABCDTORWFSDlGh/5xzv2nxZJDzEpBnblB11NFRPYd1jaB3wQIS1iv32GDo4MCAggA";
    }
}
