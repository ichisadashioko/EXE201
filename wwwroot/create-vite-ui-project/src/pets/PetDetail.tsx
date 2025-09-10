import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router"
import { api_get_pet_info, getAccessToken } from "../authentication";

import './PetDetail.css';

export default function PetDetail() {
    const { petId } = useParams<{ petId: string }>();
    const navigate = useNavigate();
    const access_token = getAccessToken();

    const [name, setName] = useState<string>("");
    const [description, setDescription] = useState<string>("");
    if (access_token == null) {
        console.log("Access token is null, redirecting to login page");
        navigate("/login");
        return null;
    }

    if (petId == null) {
        console.error("petId is null, redirecting to home page");
        navigate("/login");
        return null;
    }

    useEffect(() => {
        const fetchPetDetails = async () => {
            try {
                const retval = await api_get_pet_info(
                    access_token,
                    petId,
                );

                console.debug(retval);

                if (retval.success) {
                    console.log("Pet info:", retval.data);
                    // TODO display pet info
                    // alert(`Pet info: ${JSON.stringify(retval.data)}`);
                    try {
                        let pet_name = retval.data.name;
                        let description = retval.data.description;
                        setName(pet_name);
                        setDescription(description);
                    } catch (error) {
                        console.error("Error fetching pet info:");
                        console.error(error);
                        alert(`Error fetching pet info: ${error}`);
                    }
                } else {
                    console.error("Failed to get pet info:", retval.message);
                    alert(`Failed to get pet info: ${retval.message}`);
                    // navigate("/home");
                }
            } catch (error) {
                console.error("Error fetching pet info:");
                console.error(error);
                alert(`Error fetching pet info: ${error}`);
                // navigate("/home");
            }
        };

        fetchPetDetails();
    });
    return (
        <div>
            <h1>Pet Detail Page - TODO</h1>
            <div>
                <h3>Name:</h3>
                <p className="red">{name}</p>
            </div>
            <div>
                <h3>Description:</h3>
                <p>{description}</p>
            </div>
        </div>
    )
}
