import { useNavigate } from "react-router";
import { api_create_new_pet, getAccessToken } from "../authentication";
import { useEffect } from "react";

export default function NewPet() {
    const navigate = useNavigate();
    const access_token = getAccessToken();

    useEffect(() => {
        if (access_token == null) {
            console.log("Access token is null, redirecting to login page");
            navigate("/login");
        }
    });

    return (
        <div>
            <h1>Create New Pet</h1>
            <div>
                <h2>Enter your pet name</h2>
            </div>
            <div>
                <input id="input_pet_name" type="text" placeholder="Pet Name" />
            </div>
            <div>
                <button onClick={() => {
                    console.log("Create Pet clicked");
                    let pet_name_input = (document.getElementById("input_pet_name") as HTMLInputElement);
                    if (pet_name_input == null) {
                        console.error("Pet name input element not found");
                        return;
                    }

                    if (access_token == null) {
                        console.error("Access token is null, cannot create pet");
                        navigate("/login");
                        return;
                    }

                    let pet_name = pet_name_input.value;
                    console.log(`Pet name: ${pet_name}`);
                    // TODO call API to create pet
                    api_create_new_pet(
                        access_token,
                        pet_name,
                    ).then((response) => {
                        console.log("Create pet response:", response);
                        if (response.success) {
                            try {
                                // TODO redirect to pet page given response data
                                let pet_id = response.data.pet.id;
                                // alert("Pet created successfully");
                                navigate(`/pets/${pet_id}`);
                            } catch (error) {
                                console.error('problem getting pet id from response data:', response);
                                console.error(error);
                                // TODO use better alert for UX
                                alert("Pet created but problem getting pet id from response data, see console for details");
                            }
                        } else {
                            alert(`Failed to create pet: ${response.message}`);
                        }
                    });
                }}>Create Pet</button>
            </div>
        </div>
    )
}
