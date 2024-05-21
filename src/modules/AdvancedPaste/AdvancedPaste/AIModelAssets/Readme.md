## Setting up the ML models

PowerToys Advanced Paste uses ML models for on device AI actions such as transcribing an audio or video file. Before you can use these features, you will need to download the models and place them in the right directory before building the project.

Here is how your directory structure should look like with the models included

```
src\modules\AdvancedPaste\AdvancedPaste
├── AIModelAssets
│   ├── whisper
│   |   ├── silero_vad.onnx
│   │   ├── whisper-small.onnx
```

There are two models that are used in this project.
1. Silero VAD - a voice activity detection model that is used to detect speech in an audio file and used to chunk a long audio file into smaller segments
2. Whisper Small - an automatic speech recognition model that is used to transcribe the audio or audio file segments

### Silero VAD

1. Download the pre-trained model from this [GitHub repo](https://github.com/snakers4/silero-vad). The model is available in the `files` directory, named `silero_vad.onnx`.
2. Place the `silero_vad.onnx` file in the `src\modules\AdvancedPaste\AdvancedPaste\AIModelAssets\whisper` directory. Create the directory if it does not exist.

### Whisper

1. Generate an optimized model with Olive [following these instructions](https://github.com/microsoft/Olive/blob/main/examples/whisper/README.md). Here the commands we used to generate the model (assuming you already have python installed):
    ``` bash
    # Clone the Olive repository and navigate to the whisper example folder
    git clone https://github.com/microsoft/Olive
    cd Olive/examples/whisper

    # Install the required packages
    pip install olive-ai
    python -m pip install -r requirements.txt
    pip install onnxruntime onnxruntime_extensions

    # prepare the whisper model (note, you can use other whisper variants as well, e.g. whisper-tiny)
    python prepare_whisper_configs.py --model_name openai/whisper-small --multilingual --enable_timestamps 

    # Run the Olive workflow to generate the optimized model
    olive run --config whisper_cpu_int8.json --setup
    olive run --config whisper_cpu_int8.json
    ```


    The generated model will be in the `.\models\conversion-transformers_optimization-onnx_dynamic_quantization-insert_beam_search-prepost` folder. 

2. Rename the `whisper_cpu_int8_cpu-cpu_model.onnx` file that was generated to `whisper-small.onnx`and place it in the `src\modules\AdvancedPaste\AdvancedPaste\AIModelAssets\whisper` directory. Create the directory if it does not exist.

