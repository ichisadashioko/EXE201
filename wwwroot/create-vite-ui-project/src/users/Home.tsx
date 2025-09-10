import { useEffect, useState } from "react"
import api_get_user_profile, { getAccessToken } from "../authentication";
import { useNavigate } from "react-router";

export default function Home() {
    // TODO fetch user profile and display user name
    // const [user, setUser] =
    let [tmp_user_profile_json_obj, set_tmp_user_profile_json_obj] = useState<any>(null);
    const navigate = useNavigate();
    const access_token = getAccessToken();
    if (access_token == null) {
        console.log("Access token is null, redirecting to login page");
        navigate("/login");
        return null;
    }

    useEffect(() => {
        // TODO fetch user profile
        const load_data = async () => {
            const user_profile_response = await api_get_user_profile(access_token);
            console.debug(user_profile_response);
            if (user_profile_response.success) {
                set_tmp_user_profile_json_obj(user_profile_response);
            } else {
                console.error("Failed to fetch user profile:", user_profile_response.message);
                alert(`Failed to fetch user profile: ${user_profile_response.message}`);
                navigate("/login");
            }
        };

        load_data();
    }, [])
    return (
        <div>
            <h1>Welcome, TODO user display name</h1>
            <pre id="tmp_profile_json">{JSON.stringify(tmp_user_profile_json_obj)}</pre>
            {/* <div><a href="/"><button>Logout</button></a> </div> */}
            <button onClick={() => {
                console.log("create new pet clicked");
                navigate("/pets/create");
            }}>Create New Pet</button>
        </div>
    )
}
