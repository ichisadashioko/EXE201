import { useNavigate } from "react-router";
import { api_pets_matching, getAccessToken } from "../authentication";
import { useEffect, useState } from "react";

export default function Matching() {
    const navigate = useNavigate();
    const access_token = getAccessToken();
    const [pet_info_list, setPetInfoList] = useState<any[]>([]);

    useEffect(() => {
        if (access_token == null) {
            console.log("Access token is null, redirecting to login page");
            navigate("/login");
            return;
        }

        async function fetchMatchingPets() {
            let retval = await api_pets_matching(
                access_token!,
            );

            console.debug(retval);

            if (retval.success) {
                try {
                    let pet_list = retval.data.pets;
                    setPetInfoList(pet_list);
                    console.log("Fetched matching pets:", pet_list);
                } catch (error) {
                    console.error("Error fetching matching pets:");
                    console.error(error);
                    alert(`Error fetching matching pets: ${error}`);
                    return;
                }
                // alert("Image uploaded successfully");
            } else {
                alert(`Failed to fetch matching pets: ${retval.message}`);
            }
        }

        fetchMatchingPets();
    }, []);
    return (
        <div>
            <h1>Matching</h1>
            <pre>{JSON.stringify(pet_info_list, null, 4)}</pre>
        </div>
    )
}
