import { useState } from "react";
import { useNavigate } from "react-router";
import api from "./authentication";

export default function Login() {
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const navigate = useNavigate();

    const handleSubmit = async (event: React.FormEvent) => {
        event.preventDefault();

        api.api_login_with_email(email, password).then((retval) => {
            console.debug(retval);
            if (retval.success) {
                const access_token = retval.data.access_token;
                if (access_token == null) {
                    alert("Login failed: Internal Server Error: access_token is null");
                    return;
                }

                api.storeAccessToken(access_token);
                console.debug("Access Token:", access_token);
                // TODO how to redirect to home page?
                navigate("/home");
            } else {
                const error_message = `Login failed: ${retval.status_code} ${retval.message} - ${retval.data}`;
                console.error(error_message);
                alert(error_message);
            }
        }).catch((error) => {
            console.error("Error during login:");
            console.error(error);
            alert(`Login failed: ${error}`);
            // alert(`Login failed: ${retval.message}`);
            // console.error("Error storing matching record:");
            // console.error(error);
            // alert(`Error storing matching record: ${error}`);
        });

        //----------------------------------------------------------
        // const response = await fetch("/api/users/login_with_email", {
        //     method: "POST",
        //     headers: {
        //         "Content-Type": "application/json",
        //     },
        //     body: JSON.stringify({ email, password }),
        // });

        // if (response.ok) {
        //     const data = await response.json();
        //     if (data == null) {
        //         alert("Login failed: Internal Server Error: response data is null");
        //         return;
        //     }
        //     const access_token = data.access_token;
        //     if (access_token == null) {
        //         alert("Login failed: Internal Server Error: access_token is null");
        //         return;
        //     }
        // api.storeAccessToken(access_token);
        // alert("Login successful!");
        // console.log("Access Token:", data.access_token);
        // TODO how to redirect to home page?
        // navigate("/home");
        // } else {
        //         const response_text = await response.text();
        //     const error_message = `Login failed: ${response.status} ${response.statusText} - ${response_text}`;
        //     console.error(error_message);
        //     alert(error_message);
        //     // alert(data.message || "Login failed");
        // }
    };

    return (
        <div>
            <h1>Login Page</h1>
            <form onSubmit={handleSubmit}>
                <div>
                    <label htmlFor="email">Email:</label>
                    <input
                        type="email"
                        id="email"
                        value={email}
                        onChange={(e) => setEmail(e.target.value)}
                        required
                    />
                </div>
                <div>
                    <label htmlFor="password">Password:</label>
                    <input
                        type="password"
                        id="password"
                        value={password}
                        onChange={(e) => setPassword(e.target.value)}
                        required
                    />
                </div>
                <button type="submit">Login</button>
            </form>
        </div>
    );
}
